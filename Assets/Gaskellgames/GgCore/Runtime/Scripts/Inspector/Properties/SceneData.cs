using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [System.Serializable]
    public class SceneData : ISerializationCallbackReceiver, IComparable<SceneData>
    {
        #region ISerializationCallbackReceiver
        
        public void OnBeforeSerialize()
        {
#if UNITY_EDITOR
            // keep asset references up to date for use at runtime
            if (sceneAsset != null)
            {
                this.sceneName = sceneAsset.name;
                this.sceneFilePath = UnityEditor.AssetDatabase.GetAssetOrScenePath(sceneAsset);
                this.guid = UnityEditor.AssetDatabase.AssetPathToGUID(sceneFilePath);
                this.buildIndex = SceneUtility.GetBuildIndexByScenePath(sceneFilePath);
            }
            else
            {
                this.sceneName = null;
                this.sceneFilePath = null;
                this.guid = null;
                this.buildIndex = -1;
            }
#endif
        }

        public void OnAfterDeserialize()
        {
            // blank
        }

        #endregion

        //----------------------------------------------------------------------------------------------------
        
        #region IComparable

        public int CompareTo(SceneData otherSceneData)
        {
            return string.Compare(otherSceneData.SceneFilePath, sceneFilePath, StringComparison.Ordinal);
        }
        
        public override bool Equals(object obj)
        {
            return obj == null
                ? string.IsNullOrEmpty(sceneFilePath)
                : obj is SceneData sceneData && sceneData.SceneFilePath == sceneFilePath;
        }
 
        public override int GetHashCode()
        {
            return SceneFilePath.GetHashCode();
        }
        
        // define the 'is equal to' operator
        public static bool operator == (SceneData sceneData1, SceneData sceneData2)
        {
            if (sceneData1?.SceneFilePath != null && sceneData2?.SceneFilePath != null)
            {
                return sceneData1.CompareTo(sceneData2) == 0;
            }
            else if (sceneData1?.SceneFilePath == null && sceneData2?.SceneFilePath != null)
            {
                return false;
            }
            else if (sceneData1?.SceneFilePath != null && sceneData2?.SceneFilePath == null)
            {
                return false;
            }
            else 
            {
                return true;
            }
        }
        
        // define the 'is not equal to' operator
        public static bool operator != (SceneData sceneData1, SceneData sceneData2)
        {
            if (sceneData1?.SceneFilePath != null && sceneData2?.SceneFilePath != null)
            {
                return sceneData1.CompareTo(sceneData2) < 0 || 0 < sceneData1.CompareTo(sceneData2);
            }
            else if (sceneData1?.SceneFilePath == null && sceneData2?.SceneFilePath != null)
            {
                return true;
            }
            else if (sceneData1?.SceneFilePath != null && sceneData2?.SceneFilePath == null)
            {
                return true;
            }
            else 
            {
                return false;
            }
        }

        #endregion

        //----------------------------------------------------------------------------------------------------

        #region Variables

        [SerializeField]
        [Tooltip("The build index of the scene.")]
        private int buildIndex;

        [SerializeField]
        [Tooltip("The name of the scene.")]
        private string sceneName;

        [SerializeField]
        [Tooltip("The file path of the scene.")]
        private string sceneFilePath;

        [SerializeField]
        [Tooltip("The guid of the scene.")]
        private string guid;
        
#if UNITY_EDITOR
        [SerializeField]
        [Tooltip("EditorOnly reference to the scene asset.")]
        private UnityEditor.SceneAsset sceneAsset;
#endif
        
        #endregion

        //----------------------------------------------------------------------------------------------------

        #region Getter / Setter

        /// <summary>
        /// Get the GUID for the SceneData's SceneAsset
        /// </summary>
        public string Guid
        {
            get => guid;
            private set => guid = value;
        }

        /// <summary>
        /// Get the BuildIndex for the SceneData's SceneAsset
        /// </summary>
        public int BuildIndex
        {
            get => buildIndex;
            private set => buildIndex = value;
        }

        /// <summary>
        /// Get the FileName for the SceneData's SceneAsset
        /// </summary>
        public string SceneName
        {
            get => sceneName;
            private set => sceneName = value;
        }

        /// <summary>
        /// Get the FilePath for the SceneData's SceneAsset
        /// </summary>
        public string SceneFilePath
        {
            get => sceneFilePath;
            private set => sceneFilePath = value;
        }
        
        /// <summary>
        /// Whether this is a valid Scene.
        /// A scene will be valid at runtime if the build index is not -1.
        /// A Scene will be valid in the editor if the sceneFilePath and sceneName are valid.
        /// </summary>
        public bool IsValid
        {
            get
            {
#if UNITY_EDITOR
                // editor only check
                if (Application.isEditor)
                {
                    if (string.IsNullOrEmpty(sceneName) || string.IsNullOrEmpty(sceneFilePath))
                    {
                        return false;
                    }
                    return File.Exists(sceneFilePath);
                }
#endif
                
                // runtime check
                return 0 < SceneUtility.GetBuildIndexByScenePath(sceneFilePath);
            }
        }

        /// <summary>
        /// Get the Scene struct from the SceneManager for this SceneData. WARNING: Returned Scene will only be valid if the scene is loaded!
        /// </summary>
        public Scene Scene => SceneManager.GetSceneByPath(sceneFilePath);
        
#if UNITY_EDITOR
        /// <summary>
        /// Get the SceneAsset for this SceneData [can only be accessed in Editor]
        /// </summary>
        public UnityEditor.SceneAsset SceneAsset
        {
            get => sceneAsset;
            set => sceneAsset = value;
        }
#endif

        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region Contructors

        /// <summary>
        /// SceneData uses a SceneAsset in the editor to cache the values of buildIndex, sceneName, sceneFilePath and guid.
        /// New SceneData can be created at runtime from: a scene, a guid, a buildIndex or a sceneFilePath.
        /// </summary>
        public SceneData()
        {
#if UNITY_EDITOR
            this.sceneAsset = null;
#endif
            this.buildIndex = -1;
            this.sceneName = null;
            this.sceneFilePath = null;
            this.guid = null;
        }
        
        /// <summary>
        /// SceneData uses a SceneAsset in the editor to cache the values of buildIndex, sceneName, sceneFilePath and guid.
        /// New SceneData can be created at runtime from: a scene, a guid, a buildIndex or a sceneFilePath.
        /// </summary>
        /// <param name="sceneData"></param>
        public SceneData(SceneData sceneData)
        {
#if UNITY_EDITOR
            this.sceneAsset = sceneData.SceneAsset;
#endif
            this.buildIndex = sceneData.BuildIndex;
            this.sceneName = sceneData.SceneName;
            this.sceneFilePath = sceneData.SceneFilePath;
            this.guid = sceneData.Guid;
        }
        
        /// <summary>
        /// SceneData uses a SceneAsset in the editor to cache the values of buildIndex, sceneName, sceneFilePath and guid.
        /// New SceneData can be created at runtime from: a scene, a guid, a buildIndex or a sceneFilePath.
        /// </summary>
        /// <param name="scene"></param>
        public SceneData(Scene scene)
        {
            if (scene.IsValid())
            {
                string filePath = scene.path;

#if UNITY_EDITOR
                this.sceneAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(filePath);
                this.guid = UnityEditor.AssetDatabase.AssetPathToGUID(filePath);
#endif
                this.buildIndex = SceneUtility.GetBuildIndexByScenePath(filePath);
                this.sceneName = Path.GetFileNameWithoutExtension(filePath);
                this.sceneFilePath = filePath;
            }
            else
            {
#if UNITY_EDITOR
                this.sceneAsset = null;
#endif
                this.guid = null;
                this.buildIndex = -1;
                this.sceneName = null;
                this.sceneFilePath = null;
            }
        }
        
        /// <summary>
        /// SceneData uses a SceneAsset in the editor to cache the values of buildIndex, sceneName, sceneFilePath and guid.
        /// New SceneData can be created at runtime from: a scene, a guid, a buildIndex or a sceneFilePath.
        /// </summary>
        /// <param name="buildIndex"></param>
        public SceneData(int buildIndex)
        {
            if (0 <= buildIndex && buildIndex < UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings)
            {
                string filePath = SceneUtility.GetScenePathByBuildIndex(buildIndex);

#if UNITY_EDITOR
                this.sceneAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(filePath);
                this.guid = UnityEditor.AssetDatabase.AssetPathToGUID(filePath);
#endif
                this.buildIndex = buildIndex;
                this.sceneName = Path.GetFileNameWithoutExtension(filePath);
                this.sceneFilePath = filePath;
            }
            else
            {
#if UNITY_EDITOR
                this.sceneAsset = null;
#endif
                this.guid = null;
                this.buildIndex = -1;
                this.sceneName = null;
                this.sceneFilePath = null;
            }
        }
        
        /// <summary>
        /// SceneData uses a SceneAsset in the editor to cache the values of buildIndex, sceneName, sceneFilePath and guid.
        /// New SceneData can be created at runtime from: a scene, a guid, a buildIndex or a sceneFilePath.
        /// </summary>
        /// <param name="sceneFilePath"></param>
        public SceneData(string sceneFilePath)
        {
            Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(sceneFilePath);
            if (scene.IsValid())
            {
#if UNITY_EDITOR
                this.sceneAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(sceneFilePath);
                this.guid = UnityEditor.AssetDatabase.AssetPathToGUID(sceneFilePath);
#endif
                this.buildIndex = SceneUtility.GetBuildIndexByScenePath(sceneFilePath);
                this.sceneName = Path.GetFileNameWithoutExtension(sceneFilePath);
                this.sceneFilePath = sceneFilePath;
            }
            else
            {
#if UNITY_EDITOR
                this.sceneAsset = null;
#endif
                this.guid = null;
                this.buildIndex = -1;
                this.sceneName = null;
                this.sceneFilePath = null;
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// SceneData uses a SceneAsset in the editor to cache the values of buildIndex, sceneName, sceneFilePath and guid.
        /// New SceneData can be created at runtime from: a scene, a guid, a buildIndex or a sceneFilePath.
        /// </summary>
        /// <param name="sceneAsset"></param>
        public SceneData(UnityEditor.SceneAsset sceneAsset)
        {
            if (sceneAsset != null)
            {
                this.sceneAsset = sceneAsset;
                this.sceneName = sceneAsset.name;
                this.sceneFilePath = UnityEditor.AssetDatabase.GetAssetOrScenePath(sceneAsset);
                this.buildIndex = SceneUtility.GetBuildIndexByScenePath(sceneFilePath);
                this.guid = UnityEditor.AssetDatabase.AssetPathToGUID(sceneFilePath);
            }
            else
            {
                this.sceneAsset = null;
                this.sceneName = null;
                this.sceneFilePath = null;
                this.buildIndex = -1;
                this.guid = null;
            }
        }
#endif

        #endregion

    } // class end
}
