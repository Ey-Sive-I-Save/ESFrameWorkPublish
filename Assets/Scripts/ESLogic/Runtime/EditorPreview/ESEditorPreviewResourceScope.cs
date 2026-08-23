#if UNITY_EDITOR // This editor-only implementation must remain in ES_Logic for EntityStateDomain's partial preview code.
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ES
{
    public class ESEditorPreviewResourceScopeInitializer : EditorInvoker_Level2
    {
        public override void InitInvoke()
        {
            ESEditorPreviewLifecycleHub.RegisterGlobalHooks();
        }
    }

    /// <summary>
    /// 编辑器预览临时资源生命周期容器。
    /// 用于统一登记并释放预览中创建的隐藏 GameObject、RenderTexture 和自定义资源。
    /// </summary>
    public sealed class ESEditorPreviewResourceScope : IDisposable
    {
        private readonly string owner;
        private readonly string note;
        private readonly List<UnityEngine.Object> unityObjects = new List<UnityEngine.Object>(8);
        private readonly List<Action> customDisposers = new List<Action>(4);
        private bool disposed;

        public ESEditorPreviewResourceScope(string owner, string note = null)
        {
            this.owner = string.IsNullOrEmpty(owner) ? "EditorPreview" : owner;
            this.note = note;
            ESEditorPreviewLifecycleHub.RegisterScope(this);
        }

        public bool IsDisposed => disposed;
        public int RegisteredObjectCount => unityObjects.Count;
        public int RegisteredDisposerCount => customDisposers.Count;
        public int RegisteredRenderTextureCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < unityObjects.Count; i++)
                    if (unityObjects[i] is RenderTexture texture && texture != null) count++;
                return count;
            }
        }
        public long RegisteredRenderTexturePixels
        {
            get
            {
                long pixels = 0L;
                for (int i = 0; i < unityObjects.Count; i++)
                    if (unityObjects[i] is RenderTexture texture && texture != null)
                        pixels += (long)texture.width * texture.height;
                return pixels;
            }
        }
        public long EstimatedRegisteredRenderTextureBytes
        {
            get
            {
                long bytes = 0L;
                for (int i = 0; i < unityObjects.Count; i++)
                    if (unityObjects[i] is RenderTexture texture && texture != null)
                    {
                        int samples = Math.Max(1, texture.antiAliasing);
                        int depthBytes = texture.depth > 0 ? 4 : 0;
                        bytes += (long)texture.width * texture.height * (4 + depthBytes) * samples;
                    }
                return bytes;
            }
        }

        public T RegisterObject<T>(T obj) where T : UnityEngine.Object
        {
            ThrowIfDisposed();
            if (obj == null)
                return null;

            obj.hideFlags = ESEditorPreviewUtility.PreviewHideFlags;
            unityObjects.Add(obj);
            ESEditorPreviewLifecycleHub.NotifyResourceChanged();
            return obj;
        }

        public Texture2D RegisterTexture(Texture2D texture)
        {
            return RegisterObject(texture);
        }

        public GameObject RegisterGameObject(GameObject gameObject, bool recursiveHideFlags = false)
        {
            ThrowIfDisposed();
            if (gameObject == null)
                return null;

            gameObject.hideFlags = ESEditorPreviewUtility.PreviewHideFlags;
            MarkPreviewObject(gameObject, owner, note);
            if (recursiveHideFlags)
                ESEditorPreviewUtility.SetHideFlagsRecursive(gameObject.transform, ESEditorPreviewUtility.PreviewHideFlags);

            unityObjects.Add(gameObject);
            ESEditorPreviewLifecycleHub.NotifyResourceChanged();
            return gameObject;
        }

        public RenderTexture RegisterRenderTexture(RenderTexture renderTexture)
        {
            return RegisterObject(renderTexture);
        }

        public void RegisterDisposeAction(Action disposeAction)
        {
            ThrowIfDisposed();
            if (disposeAction != null)
            {
                customDisposers.Add(disposeAction);
                ESEditorPreviewLifecycleHub.NotifyResourceChanged();
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            ESEditorPreviewLifecycleHub.UnregisterScope(this);

            for (int i = customDisposers.Count - 1; i >= 0; i--)
            {
                try
                {
                    customDisposers[i]?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
            customDisposers.Clear();

            for (int i = unityObjects.Count - 1; i >= 0; i--)
                DestroyRegisteredObject(unityObjects[i]);

            unityObjects.Clear();
            ESEditorPreviewLifecycleHub.NotifyResourceChanged();
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(ESEditorPreviewResourceScope));
        }

        public static void MarkPreviewObject(GameObject obj, string owner, string note = null)
        {
            if (obj == null)
                return;

            ESEditorPreviewUtility.MarkPreviewObject(obj, owner, note);
        }

        internal static void RegisterGlobalPreviewCleanup()
        {
            ESEditorPreviewLifecycleHub.RegisterGlobalHooks();
        }

        [MenuItem(MenuItemPathDefine.PREVIEW_CLEANUP_PATH + "清理全部预览残留对象", false, 0)]
        public static void CleanupAllMarkedPreviewObjectsMenu()
        {
            int removed = CleanupAllMarkedPreviewObjects();
            Debug.Log($"[EditorPreview] 已清理全部预览残留对象：{removed}");
        }

        public static int CleanupAllMarkedPreviewObjects()
        {
            return ESEditorPreviewUtility.CleanupAllMarkedPreviewObjects();
        }

        private static void DestroyRegisteredObject(UnityEngine.Object obj)
        {
            if (obj == null)
                return;

            ESEditorPreviewUtility.DestroyObject(obj);
        }
    }
}
#endif
