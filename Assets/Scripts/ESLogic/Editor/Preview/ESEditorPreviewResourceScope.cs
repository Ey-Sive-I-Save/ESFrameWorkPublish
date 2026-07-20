#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ES
{
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
        }

        public T RegisterObject<T>(T obj) where T : UnityEngine.Object
        {
            if (obj == null)
                return null;

            obj.hideFlags = HideFlags.HideAndDontSave;
            unityObjects.Add(obj);
            return obj;
        }

        public GameObject RegisterGameObject(GameObject gameObject, bool recursiveHideFlags = false)
        {
            if (gameObject == null)
                return null;

            gameObject.hideFlags = HideFlags.HideAndDontSave;
            MarkPreviewObject(gameObject, owner, note);
            if (recursiveHideFlags)
                SetHideFlagsRecursive(gameObject.transform, HideFlags.HideAndDontSave);

            unityObjects.Add(gameObject);
            return gameObject;
        }

        public RenderTexture RegisterRenderTexture(RenderTexture renderTexture)
        {
            return RegisterObject(renderTexture);
        }

        public void RegisterDisposeAction(Action disposeAction)
        {
            if (disposeAction != null)
                customDisposers.Add(disposeAction);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;

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
        }

        public static void MarkPreviewObject(GameObject obj, string owner, string note = null)
        {
            if (obj == null)
                return;

            EditorPreviewGameObjectSign marker = obj.GetComponent<EditorPreviewGameObjectSign>();
            if (marker == null)
                marker = obj.AddComponent<EditorPreviewGameObjectSign>();

            marker.Setup(owner, note);
            marker.hideFlags = HideFlags.HideAndDontSave;
        }

        [InitializeOnLoadMethod]
        private static void RegisterGlobalPreviewCleanup()
        {
            EditorApplication.playModeStateChanged -= CleanupOnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += CleanupOnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= CleanupAllMarkedPreviewObjectsSilent;
            AssemblyReloadEvents.beforeAssemblyReload += CleanupAllMarkedPreviewObjectsSilent;
            EditorApplication.quitting -= CleanupAllMarkedPreviewObjectsSilent;
            EditorApplication.quitting += CleanupAllMarkedPreviewObjectsSilent;
        }

        private static void CleanupOnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
                CleanupAllMarkedPreviewObjectsSilent();
        }

        private static void CleanupAllMarkedPreviewObjectsSilent()
        {
            CleanupAllMarkedPreviewObjects();
        }

        [MenuItem(MenuItemPathDefine.PREVIEW_CLEANUP_PATH + "清理全部预览残留对象", false, 0)]
        public static void CleanupAllMarkedPreviewObjectsMenu()
        {
            int removed = CleanupAllMarkedPreviewObjects();
            Debug.Log($"[EditorPreview] 已清理全部预览残留对象：{removed}");
        }

        public static int CleanupAllMarkedPreviewObjects()
        {
            int removed = 0;
            EditorPreviewGameObjectSign[] markers = Resources.FindObjectsOfTypeAll<EditorPreviewGameObjectSign>();
            for (int i = 0; i < markers.Length; i++)
            {
                EditorPreviewGameObjectSign marker = markers[i];
                if (marker == null)
                    continue;

                GameObject obj = marker.gameObject;
                if (obj == null || EditorUtility.IsPersistent(obj))
                    continue;

                UnityEngine.Object.DestroyImmediate(obj);
                removed++;
            }

            return removed;
        }

        private static void DestroyRegisteredObject(UnityEngine.Object obj)
        {
            if (obj == null)
                return;

            if (obj is RenderTexture renderTexture)
            {
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
                return;
            }

            UnityEngine.Object.DestroyImmediate(obj);
        }

        private static void SetHideFlagsRecursive(Transform root, HideFlags flags)
        {
            if (root == null)
                return;

            root.gameObject.hideFlags = flags;
            for (int i = 0; i < root.childCount; i++)
                SetHideFlagsRecursive(root.GetChild(i), flags);
        }
    }
}
#endif
