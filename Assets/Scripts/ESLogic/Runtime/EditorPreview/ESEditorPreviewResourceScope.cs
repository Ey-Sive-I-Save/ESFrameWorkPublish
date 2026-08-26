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
            // Scope 只拥有运行时创建的临时对象；项目资产、Prefab 等持久化对象
            // 必须继续由 AssetDatabase/调用方持有，禁止改 HideFlags 或在 Dispose 时销毁。
            if (EditorUtility.IsPersistent(obj))
                return obj;
            if (obj is GameObject gameObject && !ESEditorPreviewUtility.HasPreviewOwnershipFlags(gameObject))
                return obj;
            if (obj is not GameObject)
            {
                HideFlags ownershipFlags = HideFlags.HideAndDontSave
                    | HideFlags.DontSaveInEditor
                    | HideFlags.DontSaveInBuild;
                if ((obj.hideFlags & ownershipFlags) != ownershipFlags)
                    return obj;
            }
            if (unityObjects.Contains(obj))
                return obj;

            unityObjects.Add(obj);
            try
            {
                obj.hideFlags = ESEditorPreviewUtility.PreviewHideFlags;
                ESEditorPreviewLifecycleHub.NotifyResourceChanged();
                return obj;
            }
            catch
            {
                // 保留登记，确保调用方即使捕获初始化异常，后续 Dispose 仍能回收对象。
                ESEditorPreviewLifecycleHub.NotifyResourceChanged();
                throw;
            }
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
            // Prefab/场景资产不能进入临时资源所有权集合，避免 Dispose 销毁用户资产。
            if (EditorUtility.IsPersistent(gameObject))
                return gameObject;
            if (!ESEditorPreviewUtility.HasPreviewOwnershipFlags(gameObject))
                return gameObject;
            if (unityObjects.Contains(gameObject))
                return gameObject;

            unityObjects.Add(gameObject);
            try
            {
                gameObject.hideFlags = ESEditorPreviewUtility.PreviewHideFlags;
                MarkPreviewObject(gameObject, owner, note);
                if (recursiveHideFlags)
                    ESEditorPreviewUtility.SetHideFlagsRecursive(gameObject.transform, ESEditorPreviewUtility.PreviewHideFlags);

                ESEditorPreviewLifecycleHub.NotifyResourceChanged();
                return gameObject;
            }
            catch
            {
                // 保留登记，确保部分初始化失败也不会把临时对象遗忘在编辑器中。
                ESEditorPreviewLifecycleHub.NotifyResourceChanged();
                throw;
            }
        }

        public RenderTexture RegisterRenderTexture(RenderTexture renderTexture)
        {
            return RegisterObject(renderTexture);
        }

        public void RegisterDisposeAction(Action disposeAction)
        {
            ThrowIfDisposed();
            if (disposeAction != null && !customDisposers.Contains(disposeAction))
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
            Exception firstFailure = null;

            for (int i = customDisposers.Count - 1; i >= 0; i--)
            {
                try
                {
                    customDisposers[i]?.Invoke();
                    customDisposers.RemoveAt(i);
                }
                catch (Exception e)
                {
                    firstFailure ??= e;
                    Debug.LogException(e);
                }
            }

            for (int i = unityObjects.Count - 1; i >= 0; i--)
            {
                try
                {
                    DestroyRegisteredObject(unityObjects[i]);
                    unityObjects.RemoveAt(i);
                }
                catch (Exception e)
                {
                    firstFailure ??= e;
                    Debug.LogException(e);
                }
            }

            if (firstFailure != null)
            {
                // 失败项仍保留在列表中；重新注册后由下一次 CleanupAll 重试，
                // 防止一次瞬态 Unity 销毁异常把资源从治理链路中遗忘。
                disposed = false;
                ESEditorPreviewLifecycleHub.RegisterScope(this);
                ESEditorPreviewLifecycleHub.NotifyResourceChanged();
                throw new InvalidOperationException(
                    "预览资源 Scope 清理未完成，失败项已保留等待重试。", firstFailure);
            }

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
