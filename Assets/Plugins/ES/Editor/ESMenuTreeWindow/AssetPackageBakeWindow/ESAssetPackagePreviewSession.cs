#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// AssetPackage 预览业务会话。它只保存 AssetPackage 的业务参数，所有
    /// Camera、Light、RenderTexture、PreviewScene、HideFlags、Layer 和清理
    /// 均由公共 ESEditorPreviewRenderContext 负责。
    /// </summary>
    internal sealed class ESAssetPackagePreviewSession : IDisposable
    {
        private readonly ESEditorPreviewRenderContext renderContext;
        private bool disposed;
        private readonly List<GameObject> markedInstances = new List<GameObject>(4);
        private AudioListener audioListener;
        private bool audioListenerPlaying;

        public ESAssetPackagePreviewSession(
            ESEditorPreviewSceneMode sceneMode,
            ESEditorPreviewEnhancerSet enhancerSet = ESEditorPreviewEnhancerSet.Full)
        {
            renderContext = new ESEditorPreviewRenderContext("ES AssetPackage", sceneMode,
                ESEditorPreviewUtility.DefaultPreviewLayer, enhancerSet);
        }

        public Vector3 GroupOrigin => renderContext.GroupOrigin;
        public bool IsReady => !disposed && renderContext.IsReady;
        public bool UsePreviewScene => renderContext.SceneMode == ESEditorPreviewSceneMode.PreviewScene;
        public ESEditorPreviewEnhancerSet EnhancerSet => renderContext.EnhancerSet;
        public string LastStatus => disposed ? "AssetPackage preview session disposed." : renderContext.LastStatus;
        public string IsolationReport => renderContext.IsolationReport;
        public string LastObjectFlowStatus => renderContext.LastObjectFlowStatus;
        public bool CleanupMarkerAvailable => GetLiveMarkedCount() > 0;
        public string LastMarkerStatus => CleanupMarkerAvailable ? "公共 Preview ownership marker 已登记。" : "尚未登记预览对象。";
        public int MarkedObjectCount => GetLiveMarkedCount();
        public Vector3 AudioListenerOrigin => GroupOrigin;
        public Quaternion AudioListenerRotation => Quaternion.identity;

        public void Ensure()
        {
            ThrowIfDisposed();
            renderContext.Ensure();
        }

        public bool TryConfigureEnhancers(ESEditorPreviewEnhancerSet set)
        {
            ThrowIfDisposed();
            return renderContext.TryConfigureEnhancers(set);
        }

        public bool PreparePreviewObject(GameObject instance)
        {
            if (disposed || instance == null)
                return false;

            // 公共上下文只接受非持久化且已有 ownership flags 的临时对象。
            // AssetPackage 的实例化发生在业务层，这里补齐唯一的所有权入口。
            if (EditorUtility.IsPersistent(instance))
                return false;

            instance.hideFlags = ESEditorPreviewUtility.PreviewHideFlags;
            bool prepared = renderContext.PreparePreviewObject(instance, "AssetPackage preview model.", samplingTarget: true);
            instance.transform.position = GroupOrigin;
            if (!prepared)
                return false;
            markedInstances.Add(instance);
            return true;
        }

        public AudioListener EnsurePreviewAudioListener()
        {
            ThrowIfDisposed();
            Ensure();
            if (audioListener != null)
                return audioListener;

            GameObject listenerObject = ESEditorPreviewUtility.CreatePreviewGameObject(
                "ES AssetPackage Preview Audio Listener", typeof(AudioListener));
            try
            {
                if (!renderContext.PreparePreviewObject(listenerObject, "AssetPackage preview audio listener.", samplingTarget: false))
                    throw new InvalidOperationException("预览 AudioListener 未能进入公共 PreviewScene。");
                listenerObject.transform.position = AudioListenerOrigin;
                audioListener = listenerObject.GetComponent<AudioListener>();
                if (audioListener == null)
                    throw new InvalidOperationException("无法创建预览 AudioListener。");
                audioListener.enabled = false;
                return audioListener;
            }
            catch
            {
                ESEditorPreviewUtility.DestroyObject(listenerObject);
                throw;
            }
        }

        public bool PreparePreviewAudioObject(GameObject instance)
        {
            if (!PreparePreviewObject(instance))
                return false;
            instance.transform.position = GroupOrigin;
            return true;
        }

        public void SetPreviewAudioListenerPlaying(bool shouldPlay)
        {
            if (audioListener == null)
                return;
            audioListenerPlaying = shouldPlay;
            audioListener.enabled = shouldPlay;
        }

        public string GetAudioListenerDescription(AudioListener listener)
        {
            if (listener == null)
                return "未创建监听器";
            return listener == audioListener
                ? "AssetPackage 公共 Preview AudioListener（当前 " + (listener.enabled ? "启用" : "停用") + "）"
                : "外部 AudioListener（只读）";
        }

        public Texture2D RenderSnapshot(
            int width,
            int height,
            Vector3 worldCenter,
            float radius,
            float yaw,
            float pitch,
            float zoom,
            ESAssetPackagePreviewBaselinePlatform baselinePlatform)
        {
            ThrowIfDisposed();

            ESEditorPreviewQuality quality = baselinePlatform == ESAssetPackagePreviewBaselinePlatform.Fast
                ? ESEditorPreviewQuality.Fast
                : baselinePlatform == ESAssetPackagePreviewBaselinePlatform.Mobile
                    ? ESEditorPreviewQuality.Balanced
                    : ESEditorPreviewQuality.High;

            TryConfigureEnhancers(ESEditorPreviewEnhancerBudgets.ForQuality(quality));
            Ensure();

            Vector3 localCenter = renderContext.WorldToPreviewLocalPoint(worldCenter);
            ESEditorPreviewCameraPose pose = renderContext.CreateCameraPose(localCenter, radius, yaw, pitch, zoom);
            return renderContext.Snapshot(width, height, pose, quality, "ES AssetPackage Preview Snapshot");
        }

        public bool Render(
            Rect rect,
            Vector3 worldCenter,
            float radius,
            float renderScale,
            float yaw,
            float pitch,
            float zoom,
            ESAssetPackagePreviewBaselinePlatform baselinePlatform,
            double minRenderInterval)
        {
            ThrowIfDisposed();
            ESEditorPreviewQuality quality = baselinePlatform == ESAssetPackagePreviewBaselinePlatform.Fast
                ? ESEditorPreviewQuality.Fast
                : baselinePlatform == ESAssetPackagePreviewBaselinePlatform.Mobile
                    ? ESEditorPreviewQuality.Balanced
                    : ESEditorPreviewQuality.High;
            TryConfigureEnhancers(ESEditorPreviewEnhancerBudgets.ForQuality(quality));
            Ensure();
            Vector3 localCenter = renderContext.WorldToPreviewLocalPoint(worldCenter);
            ESEditorPreviewCameraPose pose = renderContext.CreateCameraPose(localCenter, radius, yaw, pitch, zoom);
            return renderContext.RenderGUI(rect, pose, new ESEditorPreviewRenderOptions(quality, renderScale, minRenderInterval));
        }

        public void Dispose()
        {
            if (disposed)
                return;

            renderContext.Dispose();
            disposed = renderContext.IsDisposed;
            if (disposed)
            {
                if (audioListener != null)
                    ESEditorPreviewUtility.DestroyObject(audioListener.gameObject);
                audioListener = null;
                audioListenerPlaying = false;
                markedInstances.Clear();
            }
        }

        private int GetLiveMarkedCount()
        {
            for (int i = markedInstances.Count - 1; i >= 0; i--)
            {
                if (markedInstances[i] == null)
                    markedInstances.RemoveAt(i);
            }

            return markedInstances.Count;
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ESAssetPackagePreviewSession));
        }
    }
}
#endif
