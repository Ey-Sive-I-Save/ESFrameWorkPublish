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
            await operation.ToUniTask(cancellationToken: cancellationToken);
            Scene scene = SceneManager.GetSceneByPath(assetPath);
            if (!scene.IsValid()) throw new InvalidOperationException("Editor scene is invalid after loading: " + assetPath);
            return scene;
        }
    }
#endif
}
