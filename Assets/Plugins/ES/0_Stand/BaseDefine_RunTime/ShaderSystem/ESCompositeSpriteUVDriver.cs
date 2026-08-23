using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace ES
{
    /// <summary>
    /// 把 Sprite 在图集中的 UV 矩形写入 Composite Shader，使局部 UV 效果不会采样相邻 Sprite。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("【ES】/相机与表现/Composite Sprite UV Driver")]
    public sealed class ESCompositeSpriteUVDriver : MonoBehaviour
    {
        private static readonly int SpriteUVRect = Shader.PropertyToID("_SpriteUVRect");
        private static readonly int SpriteUVTransformX = Shader.PropertyToID("_SpriteUVTransformX");
        private static readonly int SpriteUVTransformY = Shader.PropertyToID("_SpriteUVTransformY");
        private static readonly int SpriteUVTransformValid = Shader.PropertyToID("_SpriteUVTransformValid");
        private static readonly Vector4 FullUVRect = new Vector4(0f, 0f, 1f, 1f);
        private static readonly Vector4 IdentityTransformX = new Vector4(1f, 0f, 0f, 0f);
        private static readonly Vector4 IdentityTransformY = new Vector4(0f, 1f, 0f, 0f);

        [SerializeField] private SpriteRenderer targetSpriteRenderer;
        [SerializeField] private Image targetImage;
        [SerializeField] private bool monitorSpriteChanges = true;

        private MaterialPropertyBlock propertyBlock;
        private ESCompositeMaterialInstance materialInstance;
        private Sprite lastSprite;
        private int lastMaterialId;

        private void Reset()
        {
            ResolveTargets();
        }

        private void OnEnable()
        {
            UpdateNow();
        }

        private void LateUpdate()
        {
            if (!monitorSpriteChanges)
                return;

            Sprite sprite = ResolveSprite();
            int materialId = ResolveMaterialId();
            if (sprite != lastSprite || materialId != lastMaterialId)
                UpdateNow();
        }

        private void OnDidApplyAnimationProperties()
        {
            UpdateNow();
        }

        public void UpdateNow()
        {
            ResolveTargets();
            Sprite sprite = ResolveSprite();
            Vector4 uvRect = GetSpriteUVRect(sprite);
            bool hasTransform = TryGetSpriteUVTransform(sprite, out Vector4 transformX, out Vector4 transformY);

            if (targetSpriteRenderer != null)
            {
                Material material = targetSpriteRenderer.sharedMaterial;
                if (material != null && material.HasProperty(SpriteUVRect))
                {
                    if (propertyBlock == null)
                        propertyBlock = new MaterialPropertyBlock();
                    targetSpriteRenderer.GetPropertyBlock(propertyBlock);
                    propertyBlock.SetVector(SpriteUVRect, uvRect);
                    propertyBlock.SetVector(SpriteUVTransformX, transformX);
                    propertyBlock.SetVector(SpriteUVTransformY, transformY);
                    propertyBlock.SetFloat(SpriteUVTransformValid, hasTransform ? 1f : 0f);
                    targetSpriteRenderer.SetPropertyBlock(propertyBlock);
                }
            }
            else if (targetImage != null)
            {
                if (!ESCompositeMaterialInstance.IsCompositeMaterial(targetImage.material))
                {
                    lastSprite = sprite;
                    lastMaterialId = ResolveMaterialId();
                    return;
                }
                if (materialInstance == null)
                {
                    materialInstance = targetImage.GetComponent<ESCompositeMaterialInstance>();
                    if (materialInstance == null)
                        materialInstance = targetImage.gameObject.AddComponent<ESCompositeMaterialInstance>();
                    materialInstance.Configure(targetImage);
                }

                Material material = materialInstance.Acquire();
                if (material != null && material.HasProperty(SpriteUVRect))
                {
                    material.SetVector(SpriteUVRect, uvRect);
                    if (material.HasProperty(SpriteUVTransformX))
                        material.SetVector(SpriteUVTransformX, transformX);
                    if (material.HasProperty(SpriteUVTransformY))
                        material.SetVector(SpriteUVTransformY, transformY);
                    if (material.HasProperty(SpriteUVTransformValid))
                        material.SetFloat(SpriteUVTransformValid, hasTransform ? 1f : 0f);
                    targetImage.SetMaterialDirty();
                }
            }

            lastSprite = sprite;
            lastMaterialId = ResolveMaterialId();
        }

        public static Vector4 GetSpriteUVRect(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
                return FullUVRect;

            Vector4 outer = DataUtility.GetOuterUV(sprite);
            if (outer.z <= outer.x || outer.w <= outer.y)
                return FullUVRect;
            return outer;
        }

        /// <summary>
        /// 计算 Sprite 局部矩形 UV 到图集 UV 的仿射变换。该合同同时覆盖翻转、
        /// 旋转打包和 Tight Mesh；只保存轴对齐 Rect 无法表达这些情况。
        /// </summary>
        public static bool TryGetSpriteUVTransform(Sprite sprite, out Vector4 transformX, out Vector4 transformY)
        {
            transformX = IdentityTransformX;
            transformY = IdentityTransformY;
            if (sprite == null || sprite.texture == null)
                return false;

            Vector2[] vertices = sprite.vertices;
            Vector2[] uvs = sprite.uv;
            if (vertices == null || uvs == null || vertices.Length != uvs.Length || vertices.Length < 3)
                return false;

            Rect rect = sprite.rect;
            float pixelsPerUnit = Mathf.Max(0.0001f, sprite.pixelsPerUnit);
            float width = Mathf.Max(1f, rect.width);
            float height = Mathf.Max(1f, rect.height);
            Vector2 pivot = sprite.pivot;

            Vector2 p0 = ToLocalUV(vertices[0], pivot, pixelsPerUnit, width, height);
            for (int i = 1; i < vertices.Length - 1; i++)
            {
                Vector2 p1 = ToLocalUV(vertices[i], pivot, pixelsPerUnit, width, height);
                Vector2 delta1 = p1 - p0;
                for (int j = i + 1; j < vertices.Length; j++)
                {
                    Vector2 p2 = ToLocalUV(vertices[j], pivot, pixelsPerUnit, width, height);
                    Vector2 delta2 = p2 - p0;
                    float determinant = delta1.x * delta2.y - delta2.x * delta1.y;
                    if (Mathf.Abs(determinant) <= 0.000001f)
                        continue;

                    Vector2 uv0 = uvs[0];
                    Vector2 uv1 = uvs[i] - uv0;
                    Vector2 uv2 = uvs[j] - uv0;
                    float inverse = 1f / determinant;
                    float inverse00 = delta2.y * inverse;
                    float inverse01 = -delta2.x * inverse;
                    float inverse10 = -delta1.y * inverse;
                    float inverse11 = delta1.x * inverse;

                    float m00 = uv1.x * inverse00 + uv2.x * inverse10;
                    float m01 = uv1.x * inverse01 + uv2.x * inverse11;
                    float m10 = uv1.y * inverse00 + uv2.y * inverse10;
                    float m11 = uv1.y * inverse01 + uv2.y * inverse11;
                    float offsetX = uv0.x - m00 * p0.x - m01 * p0.y;
                    float offsetY = uv0.y - m10 * p0.x - m11 * p0.y;
                    transformX = new Vector4(m00, m01, offsetX, 0f);
                    transformY = new Vector4(m10, m11, offsetY, 0f);
                    return true;
                }
            }

            return false;
        }

        private static Vector2 ToLocalUV(Vector2 vertex, Vector2 pivot, float pixelsPerUnit, float width, float height)
        {
            return new Vector2(
                (vertex.x * pixelsPerUnit + pivot.x) / width,
                (vertex.y * pixelsPerUnit + pivot.y) / height);
        }

        private void ResolveTargets()
        {
            if (targetSpriteRenderer == null && targetImage == null)
            {
                targetSpriteRenderer = GetComponent<SpriteRenderer>();
                if (targetSpriteRenderer == null)
                    targetImage = GetComponent<Image>();
            }
        }

        private Sprite ResolveSprite()
        {
            if (targetSpriteRenderer != null)
                return targetSpriteRenderer.sprite;
            if (targetImage != null)
                return targetImage.overrideSprite != null ? targetImage.overrideSprite : targetImage.sprite;
            return null;
        }

        private int ResolveMaterialId()
        {
            Material material = null;
            if (targetSpriteRenderer != null)
                material = targetSpriteRenderer.sharedMaterial;
            else if (materialInstance != null && materialInstance.RuntimeMaterial != null)
                material = materialInstance.RuntimeMaterial;
            else if (targetImage != null)
                material = targetImage.material;
            return material != null ? material.GetInstanceID() : 0;
        }
    }
}
