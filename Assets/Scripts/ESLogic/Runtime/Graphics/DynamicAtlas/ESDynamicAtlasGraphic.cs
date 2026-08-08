using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace ES
{
    [AddComponentMenu("【ES】/场景与对象/动态图集 Graphic")]
    [DisallowMultipleComponent]
    public sealed class ESDynamicAtlasGraphic : MaskableGraphic
    {
        [Header("快速使用")]
        [Tooltip("拖入 ES 资源引用后，运行时会自动进入动态图集。")]
        [SerializeField, InlineProperty, HideLabel]
        private ESAssetReferTexture2D sourceRefer = new ESAssetReferTexture2D();

        [SerializeField, Tooltip("不填写时根据资源身份自动生成。")]
        private string contentKey;

        [SerializeField, Tooltip("头像或远端图片更新时填写新的版本号。")]
        private string contentRevision;

        [SerializeField]
        private ESDynamicAtlasDomainPreset domainPreset = ESDynamicAtlasDomainPreset.Icons;

        [SerializeField, Tooltip("选择“自定义”时使用。")]
        private string customDomainKey = "ui.runtime";

        [SerializeField]
        private ESDynamicAtlasRequest request = ESDynamicAtlasRequest.Default;

        [SerializeField]
        private bool autoAcquire = true;

        [SerializeField] private bool preserveAspect = true;
        [SerializeField] private Texture placeholderTexture;
        [SerializeField, InspectorName("材质模式")]
        private ESDynamicAtlasMaterialMode materialMode = ESDynamicAtlasMaterialMode.Auto;
        [SerializeField, InspectorName("自定义材质")]
        private Material customMaterial;
#if UNITY_EDITOR
        [SerializeField, LabelText("仅编辑器预览纹理"), Tooltip("只用于编辑器显示，不参与运行时资源加载，也不会作为动态图集源。")]
        private Texture editorPreviewTexture;
        [SerializeField, LabelText("仅编辑器预览 Sprite"), Tooltip("可直接拖入图标 Sprite；只用于编辑器显示。")]
        private Sprite editorPreviewSprite;
#endif

        private ESDynamicAtlasLease lease;
        private ESDynamicAtlasObservation observation;
        private CancellationTokenSource autoAcquireCancellation;
        private int requestRevision;
        [ShowInInspector, ReadOnly, LabelText("状态")]
        private string status = "未开始";
        private static Material premultipliedMaterial;
#if UNITY_EDITOR
        [NonSerialized] private Sprite cachedPreviewSprite;
        [NonSerialized] private Vector2[] cachedPreviewVertices;
        [NonSerialized] private Vector2[] cachedPreviewUvs;
        [NonSerialized] private ushort[] cachedPreviewTriangles;
#endif

        public bool HasContent => lease.TryResolve(out _);

        public override Texture mainTexture
        {
            get
            {
                return lease.TryResolve(out ESDynamicAtlasResolved resolved) && resolved.texture != null
                    ? resolved.texture
                    : GetEditorPreviewOrPlaceholder();
            }
        }

        public UniTask SetAsync(
            ESDynamicAtlasDomainKey domain,
            ESDynamicAtlasContentKey content,
            ESAssetReferTexture2D refer,
            CancellationToken cancellationToken = default)
        {
            return SetAsync(domain, content, refer, ESDynamicAtlasRequest.Default, cancellationToken);
        }

        public async UniTask SetAsync(
            ESDynamicAtlasDomainKey domain,
            ESDynamicAtlasContentKey content,
            ESAssetReferTexture2D refer,
            ESDynamicAtlasRequest request,
            CancellationToken cancellationToken = default)
        {
            EnsureRuntimeOnly();
            int revision = ++requestRevision;
            ESDynamicAtlasLease acquired = await ESDynamicAtlas.LoadAsync(domain, content, refer, request, cancellationToken);
            if (revision != requestRevision || !isActiveAndEnabled || this == null)
            {
                acquired.Dispose();
                return;
            }

            Bind(acquired);
        }

        public async UniTask CopyAsync(
            ESDynamicAtlasDomainKey domain,
            ESDynamicAtlasContentKey content,
            Texture texture,
            CancellationToken cancellationToken = default)
        {
            await CopyAsync(domain, content, texture, ESDynamicAtlasRequest.Default, cancellationToken);
        }

        public async UniTask CopyAsync(
            ESDynamicAtlasDomainKey domain,
            ESDynamicAtlasContentKey content,
            Texture texture,
            ESDynamicAtlasRequest request,
            CancellationToken cancellationToken = default)
        {
            EnsureRuntimeOnly();
            int revision = ++requestRevision;
            ESDynamicAtlasLease acquired = await ESDynamicAtlas.CopyAsync(domain, content, texture, request, cancellationToken);
            if (revision != requestRevision || !isActiveAndEnabled || this == null)
            {
                acquired.Dispose();
                return;
            }

            Bind(acquired);
        }

        public void Clear()
        {
            requestRevision++;
            autoAcquireCancellation?.Cancel();
            autoAcquireCancellation?.Dispose();
            autoAcquireCancellation = null;
            observation.Dispose();
            observation = default;
            lease.Dispose();
            lease = default;
            status = "未加载";
            RefreshMaterialMode();
            SetVerticesDirty();
            SetMaterialDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (Application.isPlaying && autoAcquire)
                StartAutoAcquire();
        }

        [Button("立即加载", ButtonSizes.Medium)]
        private void StartAutoAcquire()
        {
            if (!Application.isPlaying)
            {
                status = "请进入运行状态后加载";
                return;
            }

            if (sourceRefer == null || !sourceRefer.IsValid)
            {
                status = "请先拖入 ES 资源引用";
                return;
            }

            autoAcquireCancellation?.Cancel();
            autoAcquireCancellation?.Dispose();
            autoAcquireCancellation = new CancellationTokenSource();
            AcquireFromInspectorAsync(autoAcquireCancellation.Token).Forget();
        }

        private async UniTaskVoid AcquireFromInspectorAsync(CancellationToken cancellationToken)
        {
            status = "等待资源系统就绪";
            try
            {
                await ESAssets.WaitUntilReadyAsync(cancellationToken);
                status = "正在加载并上传";
                ESDynamicAtlasDomainKey domain = ResolveDomain();
                ESDynamicAtlasContentKey content = new ESDynamicAtlasContentKey(
                    ResolveContentKey(), contentRevision);
                await SetAsync(domain, content, sourceRefer, request, cancellationToken);
                status = HasContent
                    ? "已完成：正在使用动态图集"
                    : "加载完成，但当前没有可用图集内容";
            }
            catch (OperationCanceledException)
            {
                status = "已取消";
            }
            catch (System.Exception exception)
            {
                status = "加载失败：" + exception.Message;
                Debug.LogWarning("[ES动态图集] " + status, this);
            }
        }

        private ESDynamicAtlasDomainKey ResolveDomain()
        {
            switch (domainPreset)
            {
                case ESDynamicAtlasDomainPreset.Avatars:
                    return ESDynamicAtlas.UIAvatars;
                case ESDynamicAtlasDomainPreset.Custom:
                    return new ESDynamicAtlasDomainKey(string.IsNullOrWhiteSpace(customDomainKey)
                        ? "ui.runtime"
                        : customDomainKey);
                default:
                    return ESDynamicAtlas.UIIcons;
            }
        }

        private string ResolveContentKey()
        {
            if (!string.IsNullOrWhiteSpace(contentKey))
                return contentKey.Trim();

            string identity = sourceRefer?.GUID;
            if (string.IsNullOrWhiteSpace(identity))
                return "texture:unknown";

            return "texture:" + identity + ":" + sourceRefer.LocalFileId;
        }

        protected override void OnDisable()
        {
            Clear();
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            Clear();
            base.OnDestroy();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            RefreshPreviewCache();
            RefreshMaterialMode();
            if (!Application.isPlaying)
            {
                SetVerticesDirty();
                SetMaterialDirty();
            }
        }
#endif

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            bool hasResolved = lease.TryResolve(out ESDynamicAtlasResolved resolved) && resolved.texture != null;
            Texture previewTexture = hasResolved ? resolved.texture : GetEditorPreviewOrPlaceholder();
            if (!hasResolved && previewTexture == null)
                return;

            Rect rect = GetPixelAdjustedRect();
            Vector2Int pixelSize = hasResolved
                ? resolved.pixelSize
                : GetEditorPreviewPixelSize(previewTexture);
            Rect uvRect = hasResolved ? resolved.uvRect : GetEditorPreviewUv(previewTexture);
            if (preserveAspect && pixelSize.x > 0 && pixelSize.y > 0)
                PreserveAspect(ref rect, pixelSize.x / (float)pixelSize.y);

#if UNITY_EDITOR
            if (!Application.isPlaying && !hasResolved
                && editorPreviewSprite != null && editorPreviewSprite.texture == previewTexture
                && TryPopulateEditorSpriteMesh(vertexHelper, rect))
            {
                return;
            }
#endif

            Color32 vertexColor = color;
            int index = vertexHelper.currentVertCount;
            vertexHelper.AddVert(new Vector3(rect.xMin, rect.yMin), vertexColor,
                new Vector2(uvRect.xMin, uvRect.yMin));
            vertexHelper.AddVert(new Vector3(rect.xMin, rect.yMax), vertexColor,
                new Vector2(uvRect.xMin, uvRect.yMax));
            vertexHelper.AddVert(new Vector3(rect.xMax, rect.yMax), vertexColor,
                new Vector2(uvRect.xMax, uvRect.yMax));
            vertexHelper.AddVert(new Vector3(rect.xMax, rect.yMin), vertexColor,
                new Vector2(uvRect.xMax, uvRect.yMin));
            vertexHelper.AddTriangle(index, index + 1, index + 2);
            vertexHelper.AddTriangle(index + 2, index + 3, index);
        }

        private Texture GetEditorPreviewOrPlaceholder()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && editorPreviewTexture != null)
                return editorPreviewTexture;
            if (!Application.isPlaying && editorPreviewSprite != null)
                return editorPreviewSprite.texture;
#endif
            return placeholderTexture != null ? placeholderTexture : s_WhiteTexture;
        }

        private Vector2Int GetEditorPreviewPixelSize(Texture texture)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && editorPreviewSprite != null && editorPreviewSprite.texture == texture)
                return Vector2Int.RoundToInt(editorPreviewSprite.rect.size);
#endif
            return texture == null ? Vector2Int.one : new Vector2Int(texture.width, texture.height);
        }

        private Rect GetEditorPreviewUv(Texture texture)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && editorPreviewSprite != null && editorPreviewSprite.texture == texture
                && texture != null && texture.width > 0 && texture.height > 0)
            {
                try
                {
                    Rect rect = editorPreviewSprite.textureRect;
                    return new Rect(rect.x / texture.width, rect.y / texture.height,
                        rect.width / texture.width, rect.height / texture.height);
                }
                catch (System.Exception)
                {
                    return new Rect(0f, 0f, 1f, 1f);
                }
            }
#endif
            return new Rect(0f, 0f, 1f, 1f);
        }

#if UNITY_EDITOR
        private void RefreshPreviewCache()
        {
            Sprite sprite = editorPreviewSprite;
            if (ReferenceEquals(cachedPreviewSprite, sprite))
                return;

            cachedPreviewSprite = sprite;
            cachedPreviewVertices = sprite != null ? sprite.vertices : null;
            cachedPreviewUvs = sprite != null ? sprite.uv : null;
            cachedPreviewTriangles = sprite != null ? sprite.triangles : null;
        }

        private bool TryPopulateEditorSpriteMesh(VertexHelper vertexHelper, Rect rect)
        {
            RefreshPreviewCache();
            if (cachedPreviewVertices == null || cachedPreviewUvs == null
                || cachedPreviewTriangles == null
                || cachedPreviewVertices.Length == 0
                || cachedPreviewVertices.Length != cachedPreviewUvs.Length
                || cachedPreviewTriangles.Length < 3)
            {
                return false;
            }

            Bounds bounds = editorPreviewSprite.bounds;
            float width = bounds.size.x;
            float height = bounds.size.y;
            if (width <= Mathf.Epsilon || height <= Mathf.Epsilon)
                return false;

            Color32 vertexColor = color;
            int start = vertexHelper.currentVertCount;
            for (int i = 0; i < cachedPreviewVertices.Length; i++)
            {
                Vector2 vertex = cachedPreviewVertices[i];
                float x = rect.xMin + (vertex.x - bounds.min.x) / width * rect.width;
                float y = rect.yMin + (vertex.y - bounds.min.y) / height * rect.height;
                vertexHelper.AddVert(new Vector3(x, y), vertexColor, cachedPreviewUvs[i]);
            }

            for (int i = 0; i + 2 < cachedPreviewTriangles.Length; i += 3)
            {
                vertexHelper.AddTriangle(
                    start + cachedPreviewTriangles[i],
                    start + cachedPreviewTriangles[i + 1],
                    start + cachedPreviewTriangles[i + 2]);
            }

            return true;
        }
#endif

        private void Bind(ESDynamicAtlasLease acquired)
        {
            observation.Dispose();
            lease.Dispose();
            lease = acquired;
            observation = lease.Subscribe(OnAtlasChanged);
            status = lease.TryResolve(out _)
                ? "已完成：正在使用动态图集"
                : "已绑定：等待动态图集内容";
            RefreshMaterialMode();
            SetVerticesDirty();
            SetMaterialDirty();
        }

        private void OnAtlasChanged()
        {
            if (!lease.TryResolve(out _))
                status = "动态图集内容暂不可用，正在显示占位图";
            RefreshMaterialMode();
            SetVerticesDirty();
            SetMaterialDirty();
        }

        private void RefreshMaterialMode()
        {
            switch (materialMode)
            {
                case ESDynamicAtlasMaterialMode.Custom:
                    material = customMaterial;
                    return;
                case ESDynamicAtlasMaterialMode.Premultiplied:
                    material = GetPremultipliedMaterial();
                    return;
                case ESDynamicAtlasMaterialMode.Straight:
                    material = null;
                    return;
                default:
                    if (lease.TryResolve(out ESDynamicAtlasResolved resolved)
                        && resolved.alphaMode == ESDynamicAtlasAlphaMode.Premultiplied)
                    {
                        material = GetPremultipliedMaterial();
                    }
                    else
                    {
                        material = null;
                    }
                    return;
            }
        }

        private static void EnsureRuntimeOnly()
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException("ESDynamicAtlasGraphic 只能在 Play Mode 或 Player 运行时请求动态图集。编辑器请使用预览字段。 ");
        }

        private static Material GetPremultipliedMaterial()
        {
            // Editor preview must not allocate a runtime material that survives
            // a domain reload. The normal Graphic material is sufficient for
            // the source-texture preview; the premultiplied shader is created
            // only after entering Play Mode.
            if (!Application.isPlaying)
                return null;

            if (premultipliedMaterial != null)
                return premultipliedMaterial;

            Shader shader = Shader.Find("ES/UI/DynamicAtlasPremultiplied");
            if (shader == null)
                return null;

            premultipliedMaterial = new Material(shader)
            {
                name = "ES Dynamic Atlas UI Premultiplied",
                hideFlags = HideFlags.HideAndDontSave
            };
            return premultipliedMaterial;
        }

        private static void PreserveAspect(ref Rect rect, float contentAspect)
        {
            if (contentAspect <= 0f || rect.width <= 0f || rect.height <= 0f)
                return;

            float rectAspect = rect.width / rect.height;
            if (contentAspect > rectAspect)
            {
                float height = rect.width / contentAspect;
                rect.y += (rect.height - height) * 0.5f;
                rect.height = height;
            }
            else
            {
                float width = rect.height * contentAspect;
                rect.x += (rect.width - width) * 0.5f;
                rect.width = width;
            }
        }
    }
}
