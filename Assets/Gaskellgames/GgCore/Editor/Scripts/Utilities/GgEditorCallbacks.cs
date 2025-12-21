#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [InitializeOnLoad]
    public class GgEditorCallbacks : AssetPostprocessor, ISerializationCallbackReceiver
    {
        #region Variables: OnSafeInitialize

        /// <summary>
        /// Called the frame after InitializeOnLoad takes place.
        /// </summary>
        public static event Action OnSafeInitialize;
        
        #endregion
        
        #region Variables: AssemblyReload

        /// <summary>
        /// Called before AssemblyReload takes place.
        /// </summary>
        public static event Action OnBeforeAssemblyReload;

        /// <summary>
        /// Called after AssemblyReload takes place.
        /// </summary>
        public static event Action OnAfterAssemblyReload;

        #endregion
        
        #region Variables: Serialization

        /// <summary>
        /// Called before serialization takes place.
        /// </summary>
        public static event Action OnBeforeSerialization;

        /// <summary>
        /// Called after serialization takes place.
        /// </summary>
        public static event Action OnAfterSerialization;

        #endregion
        
        #region Variables: AssetPostprocessor

        /// <summary>
        /// Called when an asset has been deleted. [Passes asset as <see cref="UnityEngine.Object"/>]
        /// </summary>
        public static event OnAssetImportedDelegate OnAssetImported;
        public delegate void OnAssetImportedDelegate(Object asset, string folderPath);

        /// <summary>
        /// Called when an asset has been deleted. [Passes asset path as <see cref="string"/>]
        /// </summary>
        public static event OnAssetDeletedDelegate OnAssetDeleted;
        public delegate void OnAssetDeletedDelegate(string assetFilePath);

        /// <summary>
        /// Called when an asset has been moved. [Passes asset as <see cref="UnityEngine.Object"/> along with new asset path and old asset path as strings]
        /// </summary>
        public static event OnAssetMovedDelegate OnAssetMoved;
        public delegate void OnAssetMovedDelegate(Object asset, string newFolderPath, string oldFolderPath);

        #endregion
        
        #region Variables: ObjectChangeEvents

        /// <summary>
        /// Called when an open scene has been changed 'dirtied' without any more specific information available.
        /// This happens for example when EditorSceneManager.MarkSceneDirty is used.
        /// </summary>
        public static event OnSceneUnknownUpdateDelegate OnSceneUnknownUpdate;
        public delegate void OnSceneUnknownUpdateDelegate(Scene scene);

        /// <summary>
        /// Called when a GameObject has been created, possibly with additional objects below it in the hierarchy.
        /// </summary>
        public static event OnGameObjectCreatedDelegate OnGameObjectCreated;
        public delegate void OnGameObjectCreatedDelegate(GameObject gameObject);

        /// <summary>
        /// Called when a GameObject has been destroyed, including any parented to it in the hierarchy.
        /// </summary>
        public static event OnGameObjectDestroyedDelegate OnGameObjectDestroyed;
        public delegate void OnGameObjectDestroyedDelegate(int instanceID, GameObject parent, Scene scene);

        /// <summary>
        /// Called when the structure of a GameObject has changed and any GameObject in the hierarchy below it might have changed.
        /// </summary>
        public static event OnGameObjectHierarchyUpdatedDelegate OnGameObjectHierarchyUpdated;
        public delegate void OnGameObjectHierarchyUpdatedDelegate(GameObject gameObject);

        /// <summary>
        /// Called when the structure of a GameObject has changed.
        /// </summary>
        public static event OnGameObjectStructureUpdatedDelegate OnGameObjectStructureUpdated;
        public delegate void OnGameObjectStructureUpdatedDelegate(GameObject gameObject);

        /// <summary>
        /// Called when the parent or parent scene of a GameObject has changed.
        /// </summary>
        public static event OnGameObjectParentUpdatedDelegate OnGameObjectParentUpdated;
        public delegate void OnGameObjectParentUpdatedDelegate(GameObject gameObject, GameObject newParent, GameObject oldParent, Scene newScene, Scene oldScene);

        /// <summary>
        /// Called when the properties of a GameObject has changed.
        /// </summary>
        public static event OnGameObjectPropertiesUpdatedDelegate OnGameObjectPropertiesUpdated;
        public delegate void OnGameObjectPropertiesUpdatedDelegate(GameObject gameObject);

        /// <summary>
        /// Called when the properties of a Component has changed.
        /// </summary>
        public static event OnComponentPropertiesUpdatedDelegate OnComponentPropertiesUpdated;
        public delegate void OnComponentPropertiesUpdatedDelegate(Component component);

        /// <summary>
        /// Called when an asset object has been created.
        /// </summary>
        public static event OnAssetObjectCreatedDelegate OnAssetObjectCreated;
        public delegate void OnAssetObjectCreatedDelegate(Object asset, string assetPath);

        /// <summary>
        /// Called when an asset object has been destroyed.
        /// </summary>
        public static event OnAssetObjectDestroyedDelegate OnAssetObjectDestroyed;
        public delegate void OnAssetObjectDestroyedDelegate(int instanceID, string guid);

        /// <summary>
        /// Called when a property of an asset object in memory has changed.
        /// </summary>
        public static event OnAssetObjectPropertiesUpdatedDelegate OnAssetObjectPropertiesUpdated;
        public delegate void OnAssetObjectPropertiesUpdatedDelegate(Object asset, string assetPath);

        /// <summary>
        /// Called when a prefab instance in an open scene has been updated due to a change to the source prefab.
        /// </summary>
        public static event OnPrefabInstanceUpdatedDelegate OnPrefabInstanceUpdated;
        public delegate void OnPrefabInstanceUpdatedDelegate(GameObject prefabRoot, Scene scene);

        #endregion
        
        #region Variables: PrefabStage

        private static bool isPrefabStageOpen = false;
        
        /// <summary>
        /// Called when a prefab is opened.
        /// </summary>
        public static event OnPrefabOpenedDelegate OnPrefabStageOpened;
        public delegate void OnPrefabOpenedDelegate(GameObject rootGameObject, Scene scene);
        
        /// <summary>
        /// Called when a prefab is closing.
        /// </summary>
        public static event Action OnPrefabStageClosing;
        
        #endregion
        
        #region Variables: PlaymodeStateChange
        
        /// <summary>
        /// Called when the editor changes playmode state to EditMode.
        /// </summary>
        public static event Action OnEnteredEditMode;
        
        /// <summary>
        /// Called when the editor changes playmode state to ExitingEditMode.
        /// </summary>
        public static event Action OnExitingEditMode;
        
        /// <summary>
        /// Called when the editor changes playmode state to PlayMode.
        /// </summary>
        public static event Action OnEnteredPlayMode;
        
        /// <summary>
        /// Called when the editor changes playmode state to ExitingPlayMode.
        /// </summary>
        public static event Action OnExitingPlayMode;

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Debug Logs

        private static void DebugLog(string format, params object[] args)
        {
            GgLogs.Log(null, GgLogType.Debug, format, args);
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Constructor

        static GgEditorCallbacks()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= AssemblyReloadEvents_BeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += AssemblyReloadEvents_BeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload -= AssemblyReloadEvents_AfterAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += AssemblyReloadEvents_AfterAssemblyReload;
            
            ObjectChangeEvents.changesPublished -= ObjectChangeEvents_ChangesPublished;
            ObjectChangeEvents.changesPublished += ObjectChangeEvents_ChangesPublished;
            
            PrefabStage.prefabStageOpened -= PrefabStage_OnPrefabStageOpened;
            PrefabStage.prefabStageOpened += PrefabStage_OnPrefabStageOpened;
            
            PrefabStage.prefabStageClosing -= PrefabStage_OnPrefabStageClosing;
            PrefabStage.prefabStageClosing += PrefabStage_OnPrefabStageClosing;
            
            EditorApplication.playModeStateChanged -= EditorApplication_PlayModeStateChanged;
            EditorApplication.playModeStateChanged += EditorApplication_PlayModeStateChanged;
            
            HandleOnSafeInitialize();
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Private Functions: OnSafeInitialize

        private static async void HandleOnSafeInitialize()
        {
            if (await GgTask.WaitUntilNextFrame() != TaskResultType.Complete) { return; }
            
            DebugLog("OnSafeInitialize");
            OnSafeInitialize?.Invoke();
        }

        #endregion
        
        #region Private Functions: AssemblyReload

        private static void AssemblyReloadEvents_BeforeAssemblyReload()
        {
            DebugLog("OnBeforeAssemblyReload");
            OnBeforeAssemblyReload?.Invoke();
        }

        private static void AssemblyReloadEvents_AfterAssemblyReload()
        {
            DebugLog("OnAfterAssemblyReload");
            OnAfterAssemblyReload?.Invoke();
        }

        #endregion
        
        #region Private Functions: Serialization

        public void OnBeforeSerialize()
        {
            DebugLog("OnBeforeSerialization");
            OnBeforeSerialization?.Invoke();
        }

        public void OnAfterDeserialize()
        {
            DebugLog("OnAfterSerialization");
            OnAfterSerialization?.Invoke();
        }

        #endregion
        
        #region Private Functions: AssetPostprocessor

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            HandleImportedAssets(importedAssets);
            HandleDeletedAssets(deletedAssets);
            HandleMovedAssets(movedAssets, movedFromAssetPaths);
        }
        
        private static string FixStringSeparators(string path)
        {
            return path.Replace("\\", "/");
        }

        private static void HandleImportedAssets(string[] importedAssets)
        {
            foreach (string assetPath in importedAssets)
            {
                string folderPath = FixStringSeparators(Path.GetDirectoryName(assetPath));
                Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                if (asset == null) { continue; }
                DebugLog("OnAssetImported: {0} in folder {1}", asset.name, folderPath);
                OnAssetImported?.Invoke(asset, folderPath);
            }
        }

        private static void HandleDeletedAssets(string[] deletedAssets)
        {
            foreach (string assetPath in deletedAssets)
            {
                string filepath = FixStringSeparators(assetPath);
                DebugLog("OnAssetDeleted: {0}", filepath);
                OnAssetDeleted?.Invoke(filepath);
            }
        }

        private static void HandleMovedAssets(string[] movedAssets, string[] movedFromAssetPaths)
        {
            for (int i = 0; i < movedAssets.Length; i++)
            {
                string newFolderPath = FixStringSeparators(Path.GetDirectoryName(movedAssets[i]));
                string oldFolderPath = FixStringSeparators(Path.GetDirectoryName(movedFromAssetPaths[i]));
                Object asset = AssetDatabase.LoadAssetAtPath<Object>(movedAssets[i]);
                if (!asset) { continue; }
                DebugLog("OnAssetMoved: {0} from folder {1} to folder {2}", asset.name, oldFolderPath, newFolderPath);
                OnAssetMoved?.Invoke(asset, newFolderPath, oldFolderPath);
            }
        }

        #endregion
        
        #region Private Functions: ObjectChangeEvents

        private static void ObjectChangeEvents_ChangesPublished(ref ObjectChangeEventStream stream)
        {
            for (int index = 0; index < stream.length; ++index)
            {
                switch (stream.GetEventType(index))
                {
                    case ObjectChangeKind.ChangeScene:
                        ObjectChangeEvents_ChangeScene(ref stream, index);
                        continue;

                    case ObjectChangeKind.CreateGameObjectHierarchy:
                        ObjectChangeEvents_CreateGameObjectHierarchy(ref stream, index);
                        continue;

                    case ObjectChangeKind.DestroyGameObjectHierarchy:
                        ObjectChangeEvents_DestroyGameObjectHierarchy(ref stream, index);
                        continue;

                    case ObjectChangeKind.ChangeGameObjectStructureHierarchy:
                        ObjectChangeEvents_ChangeGameObjectStructureHierarchy(ref stream, index);
                        continue;

                    case ObjectChangeKind.ChangeGameObjectStructure:
                        ObjectChangeEvents_ChangeGameObjectStructure(ref stream, index);
                        continue;

                    case ObjectChangeKind.ChangeGameObjectParent:
                        ObjectChangeEvents_ChangeGameObjectParent(ref stream, index);
                        continue;

                    case ObjectChangeKind.ChangeGameObjectOrComponentProperties:
                        ObjectChangeEvents_ChangeGameObjectOrComponentProperties(ref stream, index);
                        continue;

                    case ObjectChangeKind.CreateAssetObject:
                        ObjectChangeEvents_CreateAssetObject(ref stream, index);
                        continue;

                    case ObjectChangeKind.DestroyAssetObject:
                        ObjectChangeEvents_DestroyAssetObject(ref stream, index);
                        continue;

                    case ObjectChangeKind.ChangeAssetObjectProperties:
                        ObjectChangeEvents_ChangeAssetObjectProperties(ref stream, index);
                        continue;

                    case ObjectChangeKind.UpdatePrefabInstances:
                        ObjectChangeEvents_UpdatePrefabInstances(ref stream, index);
                        continue;
                    
                    // handle other cases
                    case ObjectChangeKind.None:
                    default:
                        continue;
                }
            }
        }
        
        private static void ObjectChangeEvents_ChangeScene(ref ObjectChangeEventStream stream, int index)
        {
            stream.GetChangeSceneEvent(index, out ChangeSceneEventArgs changeSceneEvent);
            if (changeSceneEvent.scene == null || !changeSceneEvent.scene.IsValid()) { return; }
            
            DebugLog("OnSceneUpdated: {0}", changeSceneEvent.scene.name);
            OnSceneUnknownUpdate?.Invoke(changeSceneEvent.scene);
        }
        
        private static void ObjectChangeEvents_CreateGameObjectHierarchy(ref ObjectChangeEventStream stream, int index)
        {
            stream.GetCreateGameObjectHierarchyEvent(index, out CreateGameObjectHierarchyEventArgs createGameObjectHierarchyEvent);
            GameObject newGameObject = EditorUtility.InstanceIDToObject(createGameObjectHierarchyEvent.instanceId) as GameObject;
            if (!newGameObject) { return; }
            
            DebugLog("OnGameObjectCreated: {0}", newGameObject.name);
            OnGameObjectCreated?.Invoke(newGameObject);
        }
        
        private static void ObjectChangeEvents_DestroyGameObjectHierarchy(ref ObjectChangeEventStream stream, int index)
        {
            stream.GetDestroyGameObjectHierarchyEvent(index, out DestroyGameObjectHierarchyEventArgs destroyGameObjectHierarchyEvent);
            GameObject destroyParentGo = EditorUtility.InstanceIDToObject(destroyGameObjectHierarchyEvent.parentInstanceId) as GameObject;
            
            DebugLog("OnGameObjectDestroyed: {0} in scene {1} with parent {2}", destroyGameObjectHierarchyEvent.instanceId, destroyGameObjectHierarchyEvent.scene.name, destroyParentGo ? destroyParentGo.name : "Root");
            OnGameObjectDestroyed?.Invoke(destroyGameObjectHierarchyEvent.instanceId, destroyParentGo, destroyGameObjectHierarchyEvent.scene);
        }
        
        private static void ObjectChangeEvents_ChangeGameObjectStructureHierarchy(ref ObjectChangeEventStream stream, int index)
        {
            stream.GetChangeGameObjectStructureHierarchyEvent(index, out ChangeGameObjectStructureHierarchyEventArgs changeGameObjectStructureHierarchy);
            GameObject gameObject = EditorUtility.InstanceIDToObject(changeGameObjectStructureHierarchy.instanceId) as GameObject;
            if (!gameObject) { return; }
            
            DebugLog("OnGameObjectHierarchyUpdated: {0}", gameObject.name);
            OnGameObjectHierarchyUpdated?.Invoke(gameObject);
        }
        
        private static void ObjectChangeEvents_ChangeGameObjectStructure(ref ObjectChangeEventStream stream, int index)
        {
            stream.GetChangeGameObjectStructureEvent(index, out ChangeGameObjectStructureEventArgs changeGameObjectStructure);
            GameObject gameObjectStructure = EditorUtility.InstanceIDToObject(changeGameObjectStructure.instanceId) as GameObject;
            if (!gameObjectStructure) { return; }
            
            DebugLog("OnGameObjectStructureUpdated: {0}", gameObjectStructure.name);
            OnGameObjectStructureUpdated?.Invoke(gameObjectStructure);
        }
        
        private static void ObjectChangeEvents_ChangeGameObjectParent(ref ObjectChangeEventStream stream, int index)
        {
            stream.GetChangeGameObjectParentEvent(index, out ChangeGameObjectParentEventArgs changeGameObjectParent);
            GameObject gameObjectChanged = EditorUtility.InstanceIDToObject(changeGameObjectParent.instanceId) as GameObject;
            GameObject newParentGo = EditorUtility.InstanceIDToObject(changeGameObjectParent.newParentInstanceId) as GameObject;
            GameObject previousParentGo = EditorUtility.InstanceIDToObject(changeGameObjectParent.previousParentInstanceId) as GameObject;
            if (!gameObjectChanged) { return; }
            
            DebugLog("OnGameObjectParentUpdated: {0} parented to {1} in scene {2} from {3} in scene {4}", gameObjectChanged ? gameObjectChanged.name : "Null", newParentGo ? newParentGo.name : "Root", changeGameObjectParent.previousScene.IsValid() ? changeGameObjectParent.previousScene.name : "Null", previousParentGo ? previousParentGo.name : "Root", changeGameObjectParent.newScene.IsValid() ? changeGameObjectParent.newScene.name : "Null");
            OnGameObjectParentUpdated?.Invoke(gameObjectChanged, previousParentGo, newParentGo, changeGameObjectParent.previousScene, changeGameObjectParent.newScene);
        }
        
        private static void ObjectChangeEvents_ChangeGameObjectOrComponentProperties(ref ObjectChangeEventStream stream, int index)
        {
            stream.GetChangeGameObjectOrComponentPropertiesEvent(index, out ChangeGameObjectOrComponentPropertiesEventArgs changeGameObjectOrComponent);
            Object goOrComponent = EditorUtility.InstanceIDToObject(changeGameObjectOrComponent.instanceId);
            if (!goOrComponent) { return; }
            
            switch (goOrComponent)
            {
                case GameObject go:
                    DebugLog("OnGameObjectPropertiesUpdated: {0}", go.name);
                    OnGameObjectPropertiesUpdated?.Invoke(go);
                    break;
                case Component component:
                    DebugLog("OnComponentPropertiesUpdated: {0} on gameObject {1}", component.GetType(), component.gameObject.name);
                    OnComponentPropertiesUpdated?.Invoke(component);
                    break;
            }
        }
        
        private static void ObjectChangeEvents_CreateAssetObject(ref ObjectChangeEventStream stream, int index)
        {
            stream.GetCreateAssetObjectEvent(index, out CreateAssetObjectEventArgs createAssetObjectEvent);
            Object createdAsset = EditorUtility.InstanceIDToObject(createAssetObjectEvent.instanceId);
            if (!createdAsset) { return; }
            
            string createdAssetPath = AssetDatabase.GUIDToAssetPath(createAssetObjectEvent.guid);
            DebugLog("OnAssetObjectCreated: {0} at {1}", createdAsset.name, createdAssetPath);
            OnAssetObjectCreated?.Invoke(createdAsset, createdAssetPath);
        }
        
        private static void ObjectChangeEvents_DestroyAssetObject(ref ObjectChangeEventStream stream, int index)
        {
            stream.GetDestroyAssetObjectEvent(index, out DestroyAssetObjectEventArgs destroyAssetObjectEvent);
            DebugLog("OnAssetObjectDestroyed: {0}", destroyAssetObjectEvent.instanceId);
            OnAssetObjectDestroyed?.Invoke(destroyAssetObjectEvent.instanceId, destroyAssetObjectEvent.guid.ToString());
        }
        
        private static void ObjectChangeEvents_ChangeAssetObjectProperties(ref ObjectChangeEventStream stream, int index)
        {
            stream.GetChangeAssetObjectPropertiesEvent(index, out ChangeAssetObjectPropertiesEventArgs changeAssetObjectPropertiesEvent);
            Object changeAsset = EditorUtility.InstanceIDToObject(changeAssetObjectPropertiesEvent.instanceId);
            if (!changeAsset) { return; }
            
            string changeAssetPath = AssetDatabase.GUIDToAssetPath(changeAssetObjectPropertiesEvent.guid);
            DebugLog("OnAssetObjectPropertiesUpdated: {0} at {1}", changeAsset.name, changeAssetPath);
            OnAssetObjectPropertiesUpdated?.Invoke(changeAsset, changeAssetPath);
        }

        private static void ObjectChangeEvents_UpdatePrefabInstances(ref ObjectChangeEventStream stream, int index)
        {
            // optimisation for when the prefab stage is open: i.e we won't need to update instances.
            if (isPrefabStageOpen) { return; }
            
            stream.GetUpdatePrefabInstancesEvent(index, out UpdatePrefabInstancesEventArgs updatePrefabInstancesEvent);

            foreach (int instanceID in updatePrefabInstancesEvent.instanceIds)
            {
                GameObject prefabRoot = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
                if (!prefabRoot) { return; }

                DebugLog("OnPrefabInstanceUpdated: {0} in scene: {1}", instanceID, updatePrefabInstancesEvent.scene.name);
                OnPrefabInstanceUpdated?.Invoke(prefabRoot, updatePrefabInstancesEvent.scene);
            }
        }
        
        #endregion
        
        #region Private Functions: OnPrefabStage
        
        private static async void PrefabStage_OnPrefabStageOpened(PrefabStage prefabStage)
        {
            // Get info from the PrefabStage
            Scene scene = prefabStage.scene;

            if (await GgTask.WaitUntilNextFrame() != TaskResultType.Complete) { return; }
            if (!scene.IsValid()) { return; }
            
            isPrefabStageOpen = true;
            DebugLog("OnPrefabOpened");
            OnPrefabStageOpened?.Invoke(prefabStage.prefabContentsRoot, scene);
        }

        private static void PrefabStage_OnPrefabStageClosing(PrefabStage prefabStage)
        {
            isPrefabStageOpen = false;
            DebugLog("OnPrefabClosing");
            OnPrefabStageClosing?.Invoke();
        }
        
        #endregion
        
        #region Private Functions: PlayModeStateChanged

        private static void EditorApplication_PlayModeStateChanged(PlayModeStateChange playModeStateChange)
        {
            switch (playModeStateChange)
            {
                case PlayModeStateChange.EnteredEditMode:
                    DebugLog("OnEnteredEditMode");
                    OnEnteredEditMode?.Invoke();
                    break;
                
                case PlayModeStateChange.ExitingEditMode:
                    DebugLog("OnExitingEditMode");
                    OnExitingEditMode?.Invoke();
                    break;
                
                case PlayModeStateChange.EnteredPlayMode:
                    DebugLog("OnEnteredPlayMode");
                    OnEnteredPlayMode?.Invoke();
                    break;
                
                case PlayModeStateChange.ExitingPlayMode:
                    DebugLog("OnExitingPlayMode");
                    OnExitingPlayMode?.Invoke();
                    break;
            }
        }
        
        #endregion
        
    } // class end
}

#endif