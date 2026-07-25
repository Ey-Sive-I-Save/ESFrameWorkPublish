using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace ES
{
    /// <summary>
    /// AssetBundle physical transport. It prefers a local build, then falls back to Unity's hash-versioned bundle cache/CDN path.
    /// Retry ownership stays in ESRuntimeAssetLoader so one failed physical request is exactly one attempt.
    /// </summary>
    public sealed class ESRuntimeAssetBundleProvider : IESRuntimeAssetBundleProvider
    {
        private readonly bool allowRemoteFallback;

        public ESRuntimeAssetBundleProvider(bool allowRemoteFallback = true)
        {
            this.allowRemoteFallback = allowRemoteFallback;
        }

        public async UniTask<AssetBundle> LoadAssetBundleAsync(ESRuntimeAssetBundleRecord record, IProgress<float> progress, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(record.LocalPath) && File.Exists(record.LocalPath))
            {
                var localRequest = AssetBundle.LoadFromFileAsync(record.LocalPath, record.Crc);
                await localRequest.ToUniTask(progress: progress, cancellationToken: cancellationToken);
                if (localRequest.assetBundle != null) return localRequest.assetBundle;
                throw new InvalidOperationException($"Local AssetBundle is invalid: {record.AssetBundleKey}");
            }

            if (!string.IsNullOrEmpty(record.StreamingUrl))
            {
                using (var request = UnityWebRequestAssetBundle.GetAssetBundle(record.StreamingUrl, record.Crc))
                {
                    await request.SendWebRequest().ToUniTask(progress: progress, cancellationToken: cancellationToken);
                    if (request.result != UnityWebRequest.Result.Success)
                        throw new InvalidOperationException($"StreamingAssets AssetBundle read failed ({request.responseCode}): {record.AssetBundleKey} / {request.error}");

                    var bundle = DownloadHandlerAssetBundle.GetContent(request);
                    if (bundle == null) throw new InvalidOperationException($"StreamingAssets AssetBundle is empty: {record.AssetBundleKey}");
                    return bundle;
                }
            }

            if (!allowRemoteFallback || string.IsNullOrEmpty(record.RemoteUrl) || string.IsNullOrEmpty(record.ContentHash))
                throw new FileNotFoundException($"No local AssetBundle and no hash-versioned remote AssetBundle: {record.AssetBundleKey}");

            using (var request = UnityWebRequestAssetBundle.GetAssetBundle(record.RemoteUrl, Hash128.Parse(record.ContentHash), record.Crc))
            {
                await request.SendWebRequest().ToUniTask(progress: progress, cancellationToken: cancellationToken);
                if (request.result != UnityWebRequest.Result.Success)
                    throw new InvalidOperationException($"AssetBundle download failed ({request.responseCode}): {request.error}");

                var bundle = DownloadHandlerAssetBundle.GetContent(request);
                if (bundle == null) throw new InvalidOperationException($"Downloaded AssetBundle is empty: {record.AssetBundleKey}");
                return bundle;
            }
        }
    }
}
