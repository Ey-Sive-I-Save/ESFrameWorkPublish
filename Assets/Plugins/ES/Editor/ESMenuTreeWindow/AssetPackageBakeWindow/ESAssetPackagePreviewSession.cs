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
        private readonly ESEditorPreviewSceneMode sceneMode;
        private ESEditorPreviewEnhancerSet enhancerSet;
        private ESEditorPreviewRenderContext renderContext;
        private bool disposed;
        private string lastFailureReason;
        private readonly List<GameObject> markedInstances = new List<GameObject>(4);
        private AudioListener audioListener;
        private bool audioListenerPlaying;

        public ESAssetPackagePreviewSession(
            ESEditorPreviewSceneMode sceneMode,
            ESEditorPreviewEnhancerSet enhancerSet = ESEditorPreviewEnhancerSet.Full)
        {
            this.sceneMode = sceneMode;
            this.enhancerSet = enhancerSet;
            renderContext = CreateRenderContext();
        }

        public Vector3 GroupOrigin => renderContext.GroupOrigin;
        public bool IsReady => !disposed && renderContext != null && renderContext.IsReady;
        public bool IsDisposed => disposed || renderContext == null || renderContext.IsDisposed;
        public bool UsePreviewScene => sceneMode == ESEditorPreviewSceneMode.PreviewScene;
        public ESEditorPreviewEnhancerSet EnhancerSet => renderContext != null ? renderContext.EnhancerSet : enhancerSet;
        public string LastStatus => disposed
            ? "AssetPackage preview session disposed."
            : !string.IsNullOrEmpty(lastFailureReason) ? "AssetPackage preview failed: " + lastFailureReason
            : renderContext != null ? renderContext.LastStatus : "AssetPackage preview context not created.";
        public string IsolationReport => renderContext != null ? renderContext.IsolationReport : "AssetPackage preview context not created.";
        public string LastObjectFlowStatus => renderContext != null
            ? renderContext.LastObjectFlowStatus
            : "AssetPackage preview context not created.";
        public bool IsSceneBindingHealthy => !disposed && renderContext != null && renderContext.IsSceneBindingHealthy;
        public string SceneBindingStatus => renderContext != null ? renderContext.SceneBindingStatus : "AssetPackage preview context not created.";
        public bool CleanupMarkerAvailable => GetLiveMarkedCount() > 0;
        public string LastMarkerStatus => CleanupMarkerAvailable ? "公共 Preview ownership marker 已登记。" : "尚未登记预览对象。";
        public int MarkedObjectCount => GetLiveMarkedCount();
        public Vector3 AudioListenerOrigin => GroupOrigin;
        public Quaternion AudioListenerRotation => Quaternion.identity;

        public void Ensure()
        {
            ThrowIfDisposed();
            EnsureRenderContext();
            renderContext.Ensure();
            RebindMarkedInstances();
        }

        public bool TryEnsure(out string failureReason)
        {
            try
            {
                Ensure();
                lastFailureReason = null;
                failureReason = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                failureReason = exception.GetBaseException().Message;
                lastFailureReason = failureReason;
                return false;
            }
        }

        public bool TryConfigureEnhancers(ESEditorPreviewEnhancerSet set)
        {
            ThrowIfDisposed();
            EnsureRenderContext();
            bool configured = renderContext.TryConfigureEnhancers(set);
            if (configured)
                enhancerSet = set;
            return configured;
        }

        public bool PreparePreviewObject(GameObject instance)
        {
            if (disposed || instance == null)
                return false;

            // 公共上下文只接受非持久化且已有 ownership flags 的临时对象。
            // AssetPackage 的实例化发生在业务层，这里补齐唯一的所有权入口。
            if (EditorUtility.IsPersistent(instance))
                return false;

            if (!TryEnsure(out _))
                return false;

            try
            {
                instance.hideFlags = ESEditorPreviewUtility.PreviewHideFlags;
                bool prepared = renderContext.PreparePreviewObject(instance, "AssetPackage preview model.", samplingTarget: true);
                instance.transform.position = GroupOrigin;
                if (!prepared)
                {
                    lastFailureReason = renderContext.LastObjectFlowStatus;
                    return false;
                }
                if (!markedInstances.Contains(instance))
                    markedInstances.Add(instance);
                return true;
            }
            catch (Exception exception)
            {
                lastFailureReason = exception.GetBaseException().Message;
                return false;
            }
        }

        public AudioListener EnsurePreviewAudioListener()
        {
            ThrowIfDisposed();
            if (!TryEnsure(out string ensureError))
                throw new InvalidOperationException("预览上下文不可用：" + ensureError);
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
                if (!markedInstances.Contains(listenerObject))
                    markedInstances.Add(listenerObject);
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
            try
            {
                ThrowIfDisposed();

                ESEditorPreviewQuality quality = baselinePlatform == ESAssetPackagePreviewBaselinePlatform.Fast
                    ? ESEditorPreviewQuality.Fast
                    : baselinePlatform == ESAssetPackagePreviewBaselinePlatform.Mobile
                        ? ESEditorPreviewQuality.Balanced
                        : ESEditorPreviewQuality.High;

                TryConfigureEnhancers(ESEditorPreviewEnhancerBudgets.ForQuality(quality));
                if (!TryEnsure(out _))
                    return null;

                Vector3 localCenter = renderContext.WorldToPreviewLocalPoint(worldCenter);
                ESEditorPreviewCameraPose pose = renderContext.CreateCameraPose(localCenter, radius, yaw, pitch, zoom);
                Texture2D snapshot = renderContext.Snapshot(width, height, pose, quality, "ES AssetPackage Preview Snapshot");
                lastFailureReason = snapshot == null ? renderContext.LastStatus : null;
                return snapshot;
            }
            catch (Exception exception)
            {
                lastFailureReason = exception.GetBaseException().Message;
                return null;
            }
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
            try
            {
                ThrowIfDisposed();
                ESEditorPreviewQuality quality = baselinePlatform == ESAssetPackagePreviewBaselinePlatform.Fast
                    ? ESEditorPreviewQuality.Fast
                    : baselinePlatform == ESAssetPackagePreviewBaselinePlatform.Mobile
                        ? ESEditorPreviewQuality.Balanced
                        : ESEditorPreviewQuality.High;
                TryConfigureEnhancers(ESEditorPreviewEnhancerBudgets.ForQuality(quality));
                if (!TryEnsure(out _))
                    return false;
                Vector3 localCenter = renderContext.WorldToPreviewLocalPoint(worldCenter);
                ESEditorPreviewCameraPose pose = renderContext.CreateCameraPose(localCenter, radius, yaw, pitch, zoom);
                bool rendered = renderContext.RenderGUI(rect, pose, new ESEditorPreviewRenderOptions(quality, renderScale, minRenderInterval));
                if (rendered)
                    lastFailureReason = null;
                else if (string.IsNullOrEmpty(lastFailureReason))
                    lastFailureReason = renderContext.LastStatus;
                return rendered;
            }
            catch (Exception exception)
            {
                lastFailureReason = exception.GetBaseException().Message;
                return false;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            if (renderContext != null)
                renderContext.Dispose();
            disposed = renderContext == null || renderContext.IsDisposed;
            if (disposed)
            {
                if (audioListener != null)
                    ESEditorPreviewUtility.DestroyObject(audioListener.gameObject);
                audioListener = null;
                audioListenerPlaying = false;
                markedInstances.Clear();
            }
        }

        private ESEditorPreviewRenderContext CreateRenderContext()
        {
            return new ESEditorPreviewRenderContext(
                "ES AssetPackage",
                sceneMode,
                ESEditorPreviewUtility.DefaultPreviewLayer,
                enhancerSet);
        }

        private void EnsureRenderContext()
        {
            if (renderContext != null && !renderContext.IsDisposed)
                return;

            // 生命周期 Hub 可能直接清理了 Context，而业务 Session 尚未收到 Dispose。
            // 清掉已被 Unity 销毁或仍绑定旧场景的业务对象，让下一次入口重建完整链路。
            for (int i = markedInstances.Count - 1; i >= 0; i--)
            {
                GameObject instance = markedInstances[i];
                if (instance != null)
                {
                    try { ESEditorPreviewUtility.DestroyObject(instance); }
                    catch (Exception exception) { Debug.LogException(exception); }
                }
                markedInstances.RemoveAt(i);
            }
            audioListener = null;
            audioListenerPlaying = false;
            renderContext = CreateRenderContext();
        }

        private void RebindMarkedInstances()
        {
            for (int i = markedInstances.Count - 1; i >= 0; i--)
            {
                GameObject instance = markedInstances[i];
                if (instance == null)
                {
                    markedInstances.RemoveAt(i);
                    continue;
                }

                bool samplingTarget = audioListener == null || audioListener.gameObject != instance;
                if (!renderContext.PreparePreviewObject(instance, "AssetPackage preview session rebind.", samplingTarget))
                {
                    try { ESEditorPreviewUtility.DestroyObject(instance); }
                    catch (Exception exception) { Debug.LogException(exception); }
                    if (audioListener != null && audioListener.gameObject == instance)
                        audioListener = null;
                    markedInstances.RemoveAt(i);
                    continue;
                }
                instance.transform.position = GroupOrigin;
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
