using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// ES 编辑器预览公共工具。
    /// 只放稳定、可复用、无业务含义的底层动作，避免各窗口重复写临时对象、RT、截图和清理代码。
    /// </summary>
    public static class ESEditorPreviewUtility
    {
        public const int DefaultPreviewLayer = 31;
        public static readonly HideFlags PreviewHideFlags = HideFlags.HideAndDontSave;
        public static readonly HideFlags SamplingSafeHideFlags =
            HideFlags.HideInHierarchy
            | HideFlags.DontSaveInEditor
            | HideFlags.DontSaveInBuild
            | HideFlags.DontUnloadUnusedAsset;

        public static GameObject CreatePreviewGameObject(string name, params Type[] components)
        {
            GameObject go = components != null && components.Length > 0
                ? new GameObject(name, components)
                : new GameObject(name);
            go.hideFlags = PreviewHideFlags;
            return go;
        }

        public static void MarkPreviewObject(GameObject obj, string owner, string note = null)
        {
            if (obj == null)
                return;

            EditorPreviewGameObjectSign marker = obj.GetComponent<EditorPreviewGameObjectSign>();
            if (marker == null)
                marker = obj.AddComponent<EditorPreviewGameObjectSign>();

            marker.Setup(owner, note);
            marker.hideFlags = PreviewHideFlags;
        }

        public static bool TryMarkPreviewObject(GameObject obj, string owner, string note, out string status)
        {
            status = "Preview object marker not requested.";
            if (obj == null)
            {
                status = "Preview object marker skipped: object is null.";
                return false;
            }

            try
            {
                MarkPreviewObject(obj, owner, note);
                status = "Preview object marker registered.";
                return true;
            }
            catch (Exception ex)
            {
                status = "Preview object marker failed: " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        public static void SetHideFlagsRecursive(Transform root, HideFlags flags)
        {
            if (root == null)
                return;

            root.gameObject.hideFlags = flags;
            for (int i = 0; i < root.childCount; i++)
                SetHideFlagsRecursive(root.GetChild(i), flags);
        }

        public static void SetLayerRecursive(Transform root, int layer)
        {
            if (root == null)
                return;

            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
                SetLayerRecursive(root.GetChild(i), layer);
        }

        public static RenderTexture CreateRenderTexture(
            int width,
            int height,
            int depth,
            int antiAliasing,
            string name,
            RenderTextureFormat format = RenderTextureFormat.ARGB32)
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            var renderTexture = new RenderTexture(width, height, depth, format)
            {
                name = string.IsNullOrEmpty(name) ? "ES Editor Preview RT" : name,
                hideFlags = PreviewHideFlags,
                antiAliasing = Mathf.Max(1, antiAliasing),
                filterMode = FilterMode.Bilinear
            };
            renderTexture.Create();
            return renderTexture;
        }

        public static void ReleaseRenderTexture(ref RenderTexture renderTexture)
        {
            if (renderTexture == null)
                return;

            renderTexture.Release();
            DestroyObject(renderTexture);
            renderTexture = null;
        }

        public static Texture2D CopyTexture(Texture source, int width, int height, string name = null)
        {
            if (source == null)
                return null;

            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            RenderTexture temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;
                var copy = new Texture2D(width, height)
                {
                    name = string.IsNullOrEmpty(name) ? "ES Editor Preview Texture" : name,
                    hideFlags = PreviewHideFlags,
                    filterMode = FilterMode.Bilinear
                };
                copy.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                copy.Apply(false, false);
                return copy;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
            }
        }

        public static Texture2D RenderCameraSnapshot(Camera camera, RenderTexture renderTexture, int width, int height, string name)
        {
            if (camera == null || renderTexture == null)
                return null;

            RenderTexture oldTarget = camera.targetTexture;
            RenderTexture oldActive = RenderTexture.active;
            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                var texture = new Texture2D(width, height)
                {
                    name = string.IsNullOrEmpty(name) ? "ES Editor Preview Snapshot" : name,
                    hideFlags = PreviewHideFlags,
                    filterMode = FilterMode.Bilinear
                };
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                texture.Apply(false, false);
                return texture;
            }
            finally
            {
                camera.targetTexture = oldTarget;
                RenderTexture.active = oldActive;
            }
        }

        public static Texture2D GetAssetPreviewOrMini(UnityEngine.Object asset, Action repaintWhenLoading = null)
        {
            if (asset == null)
                return null;

            Texture2D preview = AssetPreview.GetAssetPreview(asset);
            if (preview == null && AssetPreview.IsLoadingAssetPreview(asset.GetInstanceID()))
                repaintWhenLoading?.Invoke();

            return preview != null ? preview : AssetPreview.GetMiniThumbnail(asset);
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

                DestroyObject(obj);
                removed++;
            }

            return removed;
        }

        public static bool TryConfigureUniversalCameraData(Camera camera)
        {
            if (camera == null)
                return false;

            Type dataType = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (dataType == null)
                return false;

            Component data = camera.GetComponent(dataType);
            if (data == null)
                data = camera.gameObject.AddComponent(dataType);

            SetProperty(data, "renderShadows", true);
            SetProperty(data, "requiresDepthTexture", true);
            SetProperty(data, "requiresColorTexture", true);
            SetProperty(data, "renderPostProcessing", false);
            return true;
        }

        public static Bounds CalculateBounds(GameObject root)
        {
            if (root == null)
                return new Bounds(Vector3.zero, Vector3.one);

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds bounds = new Bounds(root.transform.position, Vector3.one);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds ? bounds : new Bounds(root.transform.position, Vector3.one);
        }

        public static void EnsureRenderersEnabled(GameObject root)
        {
            if (root == null)
                return;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].enabled = true;
            }
        }

        public static void DisableRuntimeBehaviours(GameObject root)
        {
            if (root == null)
                return;

            Behaviour[] behaviours = root.GetComponentsInChildren<Behaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                Behaviour behaviour = behaviours[i];
                if (behaviour == null)
                    continue;

                if (behaviour is Animator || behaviour is Animation || behaviour is Light || behaviour is Camera)
                    continue;

                if (behaviour is EditorPreviewGameObjectSign)
                    continue;

                behaviour.enabled = false;
            }
        }

        public static void CopyRendererState(GameObject sourceRoot, GameObject previewRoot)
        {
            if (sourceRoot == null || previewRoot == null)
                return;

            Renderer[] sourceRenderers = sourceRoot.GetComponentsInChildren<Renderer>(true);
            Renderer[] previewRenderers = previewRoot.GetComponentsInChildren<Renderer>(true);
            if (sourceRenderers == null || previewRenderers == null)
                return;

            var previewMap = new Dictionary<string, Renderer>(previewRenderers.Length);
            for (int i = 0; i < previewRenderers.Length; i++)
            {
                Renderer previewRenderer = previewRenderers[i];
                if (previewRenderer == null)
                    continue;

                string key = GetRendererPathKey(previewRoot.transform, previewRenderer);
                if (!previewMap.ContainsKey(key))
                    previewMap.Add(key, previewRenderer);
            }

            var propertyBlock = new MaterialPropertyBlock();
            for (int i = 0; i < sourceRenderers.Length; i++)
            {
                Renderer sourceRenderer = sourceRenderers[i];
                if (sourceRenderer == null)
                    continue;

                string key = GetRendererPathKey(sourceRoot.transform, sourceRenderer);
                if (!previewMap.TryGetValue(key, out Renderer previewRenderer) || previewRenderer == null)
                    continue;

                previewRenderer.enabled = sourceRenderer.enabled;
                previewRenderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
                previewRenderer.receiveShadows = sourceRenderer.receiveShadows;
                previewRenderer.lightProbeUsage = sourceRenderer.lightProbeUsage;
                previewRenderer.reflectionProbeUsage = sourceRenderer.reflectionProbeUsage;
                previewRenderer.sharedMaterials = sourceRenderer.sharedMaterials;

                sourceRenderer.GetPropertyBlock(propertyBlock);
                previewRenderer.SetPropertyBlock(propertyBlock);
                propertyBlock.Clear();
            }
        }

        public static void DestroyObject(UnityEngine.Object obj)
        {
            if (obj == null)
                return;

            if (obj is RenderTexture renderTexture)
                renderTexture.Release();

            UnityEngine.Object.DestroyImmediate(obj);
        }

        private static void SetProperty(object target, string propertyName, object value)
        {
            if (target == null)
                return;

            PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null || !property.CanWrite)
                return;

            try
            {
                property.SetValue(target, value);
            }
            catch
            {
            }
        }

        private static string GetRendererPathKey(Transform root, Renderer renderer)
        {
            if (root == null || renderer == null)
                return string.Empty;

            Transform current = renderer.transform;
            string path = renderer.GetType().FullName;
            while (current != null && current != root)
            {
                path = current.GetSiblingIndex() + "/" + current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }
    }
}
