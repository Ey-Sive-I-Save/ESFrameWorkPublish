using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ES
{
#if UNITY_EDITOR
    public sealed class ESRuntimeEditorDirectAssetProvider : IESRuntimeDirectAssetProvider
    {
        public UniTask<UnityEngine.Object> LoadAsync(ESAssetIdentity id, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string assetPath = AssetDatabase.GUIDToAssetPath(id.Guid);
            if (string.IsNullOrEmpty(assetPath)) throw new InvalidOperationException("GUID does not resolve to an editor asset: " + id.Guid);
            if (!id.IsSubAsset) return UniTask.FromResult(AssetDatabase.LoadMainAssetAtPath(assetPath));

            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                if (asset != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localFileId)
                    && string.Equals(guid, id.Guid, StringComparison.Ordinal) && localFileId == id.LocalFileId)
                    return UniTask.FromResult(asset);
            throw new InvalidOperationException("GUID and LocalFileId do not resolve to an editor sub-asset: " + id);
        }

        public async UniTask<Scene> LoadSceneAsync(ESAssetIdentity id, LoadSceneMode mode, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string assetPath = AssetDatabase.GUIDToAssetPath(id.Guid);
            if (string.IsNullOrEmpty(assetPath)) throw new InvalidOperationException("GUID does not resolve to an editor scene: " + id.Guid);
            AsyncOperation operation = SceneManager.LoadSceneAsync(assetPath, mode);
            if (operation == null) throw new InvalidOperationException("Editor scene could not be loaded: " + assetPath);
            // Unity cannot stop an AsyncOperation once it has been created. Cancellation
            // only cancels the caller's wait; additive loads can be compensated after finish,
            // while Single loads must report that they cannot be rolled back.
            await operation.ToUniTask();
            Scene scene = SceneManager.GetSceneByPath(assetPath);
            if (!scene.IsValid()) throw new InvalidOperationException("Editor scene is invalid after loading: " + assetPath);
            if (cancellationToken.IsCancellationRequested)
            {
                if (mode == LoadSceneMode.Additive && scene.isLoaded)
                {
                    AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
                    if (unload == null)
                        throw new InvalidOperationException("EditorDirect 取消后的 Additive 场景补偿卸载无法启动，场景仍保持加载：" + assetPath);
                    await unload.ToUniTask();
                }
                else if (mode == LoadSceneMode.Single)
                {
                    Debug.LogWarning("[ESRes][Scene] EditorDirect 取消发生在 Single 场景加载完成后；Unity 无法回滚当前场景。Scene=" + assetPath);
                }
                throw new OperationCanceledException(cancellationToken);
            }
            return scene;
        }
    }
#endif
}
