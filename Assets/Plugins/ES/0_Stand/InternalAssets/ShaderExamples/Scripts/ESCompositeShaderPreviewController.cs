using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// ES Composite Shader 的运行时验收场景。所有动态覆盖都使用 MaterialPropertyBlock，
    /// 不修改案例材质资产；场景退出时统一销毁运行时生成对象。
    /// </summary>
    public sealed class ESCompositeShaderPreviewController : MonoBehaviour
    {
        [Header("VFX 案例材质")]
        [SerializeField] private Material sequenceMaterial;
        [SerializeField] private Material radialMaskMaterial;
        [SerializeField] private Material depthIntersectionMaterial;
        [SerializeField] private Material blendMaterial;
        [SerializeField] private Material propertyBlockMaterial;

        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        private Transform generatedRoot;
        private Renderer sequenceRenderer;
        private Renderer propertyBlockRenderer;
        private ParticleSystemRenderer particleRenderer;
        private Texture2D generatedAtlas;
        private Material generatedLitMaterial;
        private readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
        private Camera borrowedCamera;
        private Vector3 borrowedCameraPosition;
        private Quaternion borrowedCameraRotation;
        private bool borrowedCameraOrthographic;
        private float borrowedCameraOrthographicSize;
        private CameraClearFlags borrowedCameraClearFlags;
        private Color borrowedCameraBackgroundColor;
        private DepthTextureMode borrowedCameraDepthTextureMode;

        private void OnEnable()
        {
            if (Application.isPlaying) BuildPreview();
        }

        private void Update()
        {
            if (!Application.isPlaying || generatedRoot == null) return;
            float time = Time.unscaledTime;

            if (sequenceRenderer != null)
            {
                sequenceRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetTexture(MainTex, generatedAtlas);
                ES3DVFXCompositeURPProperties.SetSequence(
                    propertyBlock,
                    true,
                    ES3DVFXSequencePlaybackMode.手动帧,
                    4,
                    4,
                    Mathf.Floor(time * 8f),
                    0f);
                sequenceRenderer.SetPropertyBlock(propertyBlock);
            }

            if (propertyBlockRenderer != null)
            {
                propertyBlockRenderer.GetPropertyBlock(propertyBlock);
                float pulse = 0.5f + 0.5f * Mathf.Sin(time * 2f);
                propertyBlock.SetColor(ES3DVFXCompositeURPProperties.Color, Color.Lerp(
                    new Color(0.08f, 0.45f, 1f, 0.75f),
                    new Color(0.75f, 0.12f, 1f, 0.95f),
                    pulse));
                ES3DVFXCompositeURPProperties.SetRadialMask(
                    propertyBlock,
                    true,
                    new Vector2(0.5f, 0.5f),
                    Mathf.Lerp(0.25f, 0.68f, pulse),
                    0.08f,
                    false);
                propertyBlockRenderer.SetPropertyBlock(propertyBlock);
            }

            if (particleRenderer != null)
            {
                particleRenderer.GetPropertyBlock(propertyBlock);
                ES3DVFXCompositeURPProperties.SetVertexStreamControls(propertyBlock, true, 1f, 1f, 1f, 1f);
                propertyBlock.SetColor(ES3DVFXCompositeURPProperties.Color, new Color(0.1f, 0.8f, 1f, 0.85f));
                particleRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        private void OnDisable()
        {
            RestoreBorrowedCamera();
            if (!Application.isPlaying) return;
            if (generatedRoot != null) Destroy(generatedRoot.gameObject);
            if (generatedAtlas != null) Destroy(generatedAtlas);
            if (generatedLitMaterial != null) Destroy(generatedLitMaterial);
        }

        [ContextMenu("重建 Shader 预览")]
        private void BuildPreview()
        {
            if (!Application.isPlaying) return;
            if (generatedRoot != null) return;
            generatedRoot = new GameObject("ES Composite Shader Preview (Runtime)").transform;
            generatedRoot.SetParent(transform, false);

            CreateCamera();
            CreateLightAndDepthReference();
            generatedAtlas = CreateSequenceAtlas();

            sequenceRenderer = CreateCase("序列帧 · PropertyBlock", PrimitiveType.Quad, new Vector3(-4.2f, 1.35f, 0f), sequenceMaterial);
            Renderer radialRenderer = CreateCase("极坐标 + 径向遮罩", PrimitiveType.Quad, new Vector3(-1.4f, 1.35f, 0f), radialMaskMaterial);
            Renderer depthRenderer = CreateCase("深度交界", PrimitiveType.Sphere, new Vector3(1.35f, 1.2f, 0.15f), depthIntersectionMaterial);
            Renderer blendRenderer = CreateCase("混合模式", PrimitiveType.Quad, new Vector3(4.15f, 1.35f, 0f), blendMaterial);
            propertyBlockRenderer = CreateCase("动态参数块", PrimitiveType.Sphere, new Vector3(-1.25f, -1.55f, 0f), propertyBlockMaterial);

            if (radialRenderer != null) radialRenderer.transform.localScale = new Vector3(2.15f, 2.15f, 1f);
            if (depthRenderer != null) depthRenderer.transform.localScale = Vector3.one * 1.45f;
            if (blendRenderer != null) blendRenderer.transform.localScale = new Vector3(2.15f, 2.15f, 1f);
            if (propertyBlockRenderer != null) propertyBlockRenderer.transform.localScale = Vector3.one * 1.35f;

            CreateParticleCase(new Vector3(2.3f, -1.5f, 0f));
        }

        private Renderer CreateCase(string title, PrimitiveType primitiveType, Vector3 position, Material material)
        {
            GameObject instance = GameObject.CreatePrimitive(primitiveType);
            instance.name = title;
            instance.transform.SetParent(generatedRoot, false);
            instance.transform.localPosition = position;
            if (primitiveType == PrimitiveType.Quad) instance.transform.localScale = new Vector3(2.1f, 2.1f, 1f);
            Collider collider = instance.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            Renderer renderer = instance.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            CreateLabel(title, position + new Vector3(0f, 1.35f, 0f));
            return renderer;
        }

        private void CreateParticleCase(Vector3 position)
        {
            GameObject instance = new GameObject("粒子顶点流 · Custom1/Custom2");
            instance.transform.SetParent(generatedRoot, false);
            instance.transform.localPosition = position;
            ParticleSystem particles = instance.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.startLifetime = 2.5f;
            main.startSpeed = 0.35f;
            main.startSize = 0.65f;
            main.maxParticles = 24;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 6f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.8f;
            ParticleSystem.CustomDataModule customData = particles.customData;
            customData.enabled = true;
            customData.SetMode(ParticleSystemCustomData.Custom1, ParticleSystemCustomDataMode.Vector);
            customData.SetVectorComponentCount(ParticleSystemCustomData.Custom1, 4);
            customData.SetVector(ParticleSystemCustomData.Custom1, 0, new ParticleSystem.MinMaxCurve(0.08f));
            customData.SetVector(ParticleSystemCustomData.Custom1, 1, new ParticleSystem.MinMaxCurve(-0.04f));
            customData.SetVector(ParticleSystemCustomData.Custom1, 2, new ParticleSystem.MinMaxCurve(1f));
            customData.SetVector(ParticleSystemCustomData.Custom1, 3, new ParticleSystem.MinMaxCurve(0.22f));
            customData.SetMode(ParticleSystemCustomData.Custom2, ParticleSystemCustomDataMode.Vector);
            customData.SetVectorComponentCount(ParticleSystemCustomData.Custom2, 1);
            customData.SetVector(ParticleSystemCustomData.Custom2, 0, new ParticleSystem.MinMaxCurve(0.8f));

            particleRenderer = instance.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.sharedMaterial = propertyBlockMaterial;
            particleRenderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
            {
                ParticleSystemVertexStream.Position,
                ParticleSystemVertexStream.Normal,
                ParticleSystemVertexStream.Color,
                ParticleSystemVertexStream.UV,
                ParticleSystemVertexStream.Custom1XYZW,
                ParticleSystemVertexStream.Custom2X
            });
            CreateLabel("粒子顶点流", position + new Vector3(0f, 1.35f, 0f));
        }

        private void CreateCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObject = new GameObject("Preview Camera");
                cameraObject.tag = "MainCamera";
                cameraObject.transform.SetParent(generatedRoot, false);
                camera = cameraObject.AddComponent<Camera>();
            }
            else
            {
                borrowedCamera = camera;
                borrowedCameraPosition = camera.transform.position;
                borrowedCameraRotation = camera.transform.rotation;
                borrowedCameraOrthographic = camera.orthographic;
                borrowedCameraOrthographicSize = camera.orthographicSize;
                borrowedCameraClearFlags = camera.clearFlags;
                borrowedCameraBackgroundColor = camera.backgroundColor;
                borrowedCameraDepthTextureMode = camera.depthTextureMode;
            }
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.transform.rotation = Quaternion.identity;
            camera.orthographic = true;
            camera.orthographicSize = 4.25f;
            camera.depthTextureMode |= DepthTextureMode.Depth;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.045f, 0.065f, 1f);
        }

        private void RestoreBorrowedCamera()
        {
            if (borrowedCamera == null) return;
            borrowedCamera.transform.position = borrowedCameraPosition;
            borrowedCamera.transform.rotation = borrowedCameraRotation;
            borrowedCamera.orthographic = borrowedCameraOrthographic;
            borrowedCamera.orthographicSize = borrowedCameraOrthographicSize;
            borrowedCamera.clearFlags = borrowedCameraClearFlags;
            borrowedCamera.backgroundColor = borrowedCameraBackgroundColor;
            borrowedCamera.depthTextureMode = borrowedCameraDepthTextureMode;
            borrowedCamera = null;
        }

        private void CreateLightAndDepthReference()
        {
            GameObject lightObject = new GameObject("Preview Directional Light");
            lightObject.transform.SetParent(generatedRoot, false);
            lightObject.transform.rotation = Quaternion.Euler(35f, -25f, 0f);
            Light lightComponent = lightObject.AddComponent<Light>();
            lightComponent.type = LightType.Directional;
            lightComponent.intensity = 1.2f;

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) return;
            generatedLitMaterial = new Material(litShader) { color = new Color(0.09f, 0.11f, 0.16f, 1f) };
            GameObject depthReference = GameObject.CreatePrimitive(PrimitiveType.Cube);
            depthReference.name = "Depth Intersection Reference";
            depthReference.transform.SetParent(generatedRoot, false);
            depthReference.transform.localPosition = new Vector3(1.8f, 0.8f, 0.65f);
            depthReference.transform.localScale = new Vector3(1.35f, 2.4f, 0.4f);
            depthReference.GetComponent<Renderer>().sharedMaterial = generatedLitMaterial;
        }

        private void CreateLabel(string text, Vector3 position)
        {
            GameObject labelObject = new GameObject(text + " Label");
            labelObject.transform.SetParent(generatedRoot, false);
            labelObject.transform.localPosition = position;
            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = 0.08f;
            label.fontSize = 42;
            label.color = new Color(0.78f, 0.86f, 0.96f, 1f);
        }

        private static Texture2D CreateSequenceAtlas()
        {
            const int tileSize = 24;
            const int columns = 4;
            const int rows = 4;
            var texture = new Texture2D(tileSize * columns, tileSize * rows, TextureFormat.RGBA32, false)
            {
                name = "ES Runtime Sequence Atlas",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color[texture.width * texture.height];
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    int tileX = x / tileSize;
                    int tileY = y / tileSize;
                    int frame = tileY * columns + tileX;
                    float localX = (x % tileSize) / (float)(tileSize - 1);
                    float localY = (y % tileSize) / (float)(tileSize - 1);
                    float ring = Mathf.SmoothStep(0.16f, 0f, Mathf.Abs(Vector2.Distance(new Vector2(localX, localY), Vector2.one * 0.5f) - (0.12f + frame * 0.014f)));
                    Color frameColor = Color.HSVToRGB(frame / 16f, 0.72f, 1f);
                    pixels[y * texture.width + x] = new Color(frameColor.r, frameColor.g, frameColor.b, ring);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }
    }
}
