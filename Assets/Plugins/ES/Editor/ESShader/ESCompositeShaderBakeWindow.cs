using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ES.EditorInternal
{
    public sealed class ESCompositeShaderBakeWindow : EditorWindow, IESWindowPresentationShortTitle
    {
        public string ESWindow_PresentationShortTitle => "材质";
        private const int MaximumFrameCount = 16;
        private const int MaximumOutputDimension = 8192;
        private const long MaximumOutputPixels = 16L * 1024L * 1024L;
        private static readonly int MainTextureProperty = Shader.PropertyToID("_MainTex");
        private static readonly int SpriteUVRectProperty = Shader.PropertyToID("_SpriteUVRect");
        private static readonly int SpriteUVTransformXProperty = Shader.PropertyToID("_SpriteUVTransformX");
        private static readonly int SpriteUVTransformYProperty = Shader.PropertyToID("_SpriteUVTransformY");
        private static readonly int SpriteUVTransformValidProperty = Shader.PropertyToID("_SpriteUVTransformValid");
        private static readonly int TimeModeProperty = Shader.PropertyToID("_TimeMode");
        private static readonly int CustomTimeProperty = Shader.PropertyToID("_CustomTime");

        [SerializeField] private Material material;
        [SerializeField] private Texture2D sourceTexture;
        [SerializeField] private Sprite sourceSprite;
        [SerializeField, Range(1, MaximumFrameCount)] private int frameCount = 1;
        [SerializeField] private bool horizontalFrames = true;
        [SerializeField] private float startTime;
        [SerializeField, Min(0f)] private float frameInterval = 0.1f;
        [SerializeField] private bool importAsSprite = true;
        [SerializeField] private bool generateMipMaps;
        [SerializeField] private bool sRgb = true;
        [SerializeField] private FilterMode filterMode = FilterMode.Bilinear;

        private Texture2D bakedTexture;
        private Vector2 scroll;
        private string lastError;

        [MenuItem(MenuItemPathDefine.CONTENT_CREATION_PATH + "Shader/Composite 烘焙与导出", false, 2110)]
        private static void OpenFromMenu()
        {
            ESCompositeShaderBakeWindow window = GetWindow<ESCompositeShaderBakeWindow>();
            window.titleContent = new GUIContent("Composite 烘焙");
            window.minSize = new Vector2(420f, 520f);
            window.PopulateFromSelection();
            window.Show();
        }

        public static void Open(Material targetMaterial)
        {
            ESCompositeShaderBakeWindow window = GetWindow<ESCompositeShaderBakeWindow>();
            window.titleContent = new GUIContent("Composite 烘焙");
            window.minSize = new Vector2(420f, 520f);
            window.material = targetMaterial;
            window.ResolveTextureFromMaterial();
            window.PopulateSpriteFromSelection();
            window.Show();
            window.Focus();
        }

        public static bool IsSupportedMaterial(Material value)
        {
            return ESCompositeMaterialInstance.IsCompositeMaterial(value);
        }

        public static Vector2Int CalculateOutputSize(int frameWidth, int frameHeight, int frames, bool horizontal)
        {
            int safeFrames = Mathf.Clamp(frames, 1, MaximumFrameCount);
            int width = Mathf.Max(1, frameWidth) * (horizontal ? safeFrames : 1);
            int height = Mathf.Max(1, frameHeight) * (horizontal ? 1 : safeFrames);
            return new Vector2Int(width, height);
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Composite 烘焙");
            minSize = new Vector2(420f, 520f);
            ES.ESWindowFoundation.BindWithStandardSystemHost(
                this,
                ES.ESWindowFoundation.EnsureStandardSystemActionBar(this));
        }

        private void OnDisable()
        {
            ES.ESWindowFoundation.Unbind(this, true);
            ReleaseBakedTexture();
        }

        private void OnSelectionChange()
        {
            Repaint();
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUI.BeginChangeCheck();
            material = (Material)EditorGUILayout.ObjectField("材质", material, typeof(Material), false);
            sourceSprite = (Sprite)EditorGUILayout.ObjectField("Sprite", sourceSprite, typeof(Sprite), false);
            using (new EditorGUI.DisabledScope(sourceSprite != null))
                sourceTexture = (Texture2D)EditorGUILayout.ObjectField("源纹理", sourceTexture, typeof(Texture2D), false);

            frameCount = EditorGUILayout.IntSlider("帧数", frameCount, 1, MaximumFrameCount);
            using (new EditorGUI.DisabledScope(frameCount <= 1))
            {
                horizontalFrames = EditorGUILayout.Popup("堆叠方向", horizontalFrames ? 0 : 1, new[] { "横向", "纵向" }) == 0;
                frameInterval = Mathf.Max(0f, EditorGUILayout.FloatField("帧间隔", frameInterval));
            }
            startTime = EditorGUILayout.FloatField("起始时间", startTime);
            importAsSprite = EditorGUILayout.Toggle("导入为 Sprite", importAsSprite);
            generateMipMaps = EditorGUILayout.Toggle("生成 Mipmap", generateMipMaps);
            sRgb = EditorGUILayout.Toggle("sRGB", sRgb);
            filterMode = (FilterMode)EditorGUILayout.EnumPopup("过滤模式", filterMode);

            if (EditorGUI.EndChangeCheck())
            {
                if (sourceSprite != null)
                    sourceTexture = sourceSprite.texture;
                ReleaseBakedTexture();
                lastError = null;
            }

            DrawSourceStatus();
            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("从当前选择读取"))
                    PopulateFromSelection();
                using (new EditorGUI.DisabledScope(!CanBake(out _)))
                    if (GUILayout.Button("生成预览"))
                        BakePreview();
            }

            if (!string.IsNullOrEmpty(lastError))
                EditorGUILayout.HelpBox(lastError, MessageType.Error);

            DrawPreview();
            using (new EditorGUI.DisabledScope(bakedTexture == null))
                if (GUILayout.Button("导出 PNG", GUILayout.Height(28f)))
                    ExportPng();
            EditorGUILayout.EndScrollView();
        }

        private void DrawSourceStatus()
        {
            if (!CanBake(out string reason))
            {
                EditorGUILayout.HelpBox(reason, MessageType.Warning);
                return;
            }

            Vector2Int frameSize = ResolveFrameSize();
            Vector2Int outputSize = CalculateOutputSize(frameSize.x, frameSize.y, frameCount, horizontalFrames);
            long outputPixels = (long)outputSize.x * outputSize.y;
            MessageType type = outputSize.x > MaximumOutputDimension
                || outputSize.y > MaximumOutputDimension
                || outputPixels > MaximumOutputPixels
                ? MessageType.Warning
                : MessageType.Info;
            EditorGUILayout.HelpBox(
                "单帧 " + frameSize.x + " x " + frameSize.y
                + "，输出 " + outputSize.x + " x " + outputSize.y
                + "（RGBA32 约 " + (outputPixels * 4d / (1024d * 1024d)).ToString("0.0") + " MiB）。",
                type);
        }

        private void DrawPreview()
        {
            if (bakedTexture == null)
                return;

            float aspect = bakedTexture.width / (float)Mathf.Max(1, bakedTexture.height);
            float width = Mathf.Min(position.width - 32f, 640f);
            float height = Mathf.Clamp(width / Mathf.Max(0.01f, aspect), 96f, 420f);
            Rect rect = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(true));
            EditorGUI.DrawPreviewTexture(rect, bakedTexture, null, ScaleMode.ScaleToFit);
        }

        private bool CanBake(out string reason)
        {
            if (!IsSupportedMaterial(material))
            {
                reason = "请选择 ES Composite 材质。";
                return false;
            }

            Texture2D texture = ResolveSourceTexture();
            if (texture == null)
            {
                reason = "请选择 Sprite 或源纹理。";
                return false;
            }

            Vector2Int frameSize = ResolveFrameSize();
            Vector2Int outputSize = CalculateOutputSize(frameSize.x, frameSize.y, frameCount, horizontalFrames);
            if (outputSize.x > MaximumOutputDimension || outputSize.y > MaximumOutputDimension)
            {
                reason = "输出尺寸超过 " + MaximumOutputDimension + "，请减少帧数或降低源纹理尺寸。";
                return false;
            }
            if ((long)outputSize.x * outputSize.y > MaximumOutputPixels)
            {
                reason = "输出像素超过 " + MaximumOutputPixels.ToString("N0")
                    + "，请减少帧数或降低源纹理尺寸。";
                return false;
            }

            reason = null;
            return true;
        }

        private void BakePreview()
        {
            ReleaseBakedTexture();
            lastError = null;
            if (!CanBake(out string reason))
            {
                lastError = reason;
                return;
            }

            try
            {
                Texture2D texture = ResolveSourceTexture();
                Vector2Int frameSize = ResolveFrameSize();
                Vector2Int outputSize = CalculateOutputSize(frameSize.x, frameSize.y, frameCount, horizontalFrames);
                bakedTexture = new Texture2D(outputSize.x, outputSize.y, UnityEngine.TextureFormat.RGBA32, false, !sRgb)
                {
                    name = material.name + "_BakedPreview",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = filterMode,
                    wrapMode = TextureWrapMode.Clamp
                };

                for (int i = 0; i < frameCount; i++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar(
                            "生成 ES Composite 烘焙预览",
                            material.name + " (" + (i + 1) + "/" + frameCount + ")",
                            frameCount == 0 ? 1f : (float)i / frameCount))
                        throw new OperationCanceledException();
                    float time = startTime + frameInterval * i;
                    Texture2D frame = RenderFrame(material, texture, sourceSprite, frameSize, time, sRgb);
                    if (frame == null)
                        throw new InvalidOperationException("GPU 预览没有返回纹理。请确认当前图形设备可用。");

                    int x = horizontalFrames ? i * frameSize.x : 0;
                    int y = horizontalFrames ? 0 : i * frameSize.y;
                    bakedTexture.SetPixels32(x, y, frameSize.x, frameSize.y, frame.GetPixels32());
                    DestroyImmediate(frame);
                }
                bakedTexture.Apply(false, false);
            }
            catch (OperationCanceledException)
            {
                ReleaseBakedTexture();
                lastError = "生成预览已取消。";
            }
            catch (Exception exception)
            {
                ReleaseBakedTexture();
                lastError = exception.Message;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void ExportPng()
        {
            string defaultName = SanitizeFileName(material != null ? material.name : "ESComposite") + "_Baked.png";
            string path = EditorUtility.SaveFilePanelInProject(
                "导出 ES Composite PNG",
                defaultName,
                "png",
                "选择项目内的输出路径。");
            if (string.IsNullOrEmpty(path))
                return;

            File.WriteAllBytes(Path.GetFullPath(path), bakedTexture.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = importAsSprite ? TextureImporterType.Sprite : TextureImporterType.Default;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = generateMipMaps;
                importer.sRGBTexture = sRgb;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = filterMode;
                importer.SaveAndReimport();
            }

            Texture2D imported = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Selection.activeObject = imported;
            EditorGUIUtility.PingObject(imported);
        }

        private static Texture2D RenderFrame(
            Material sourceMaterial,
            Texture2D source,
            Sprite sprite,
            Vector2Int size,
            float time,
            bool useSrgb)
        {
            PreviewRenderUtility utility = null;
            Material runtimeMaterial = null;
            Mesh mesh = null;
            try
            {
                utility = new PreviewRenderUtility();
                utility.camera.orthographic = true;
                utility.camera.orthographicSize = 0.5f;
                utility.camera.transform.position = new Vector3(0f, 0f, -2f);
                utility.camera.transform.rotation = Quaternion.identity;
                utility.camera.nearClipPlane = 0.01f;
                utility.camera.farClipPlane = 10f;
                utility.camera.clearFlags = CameraClearFlags.Color;
                utility.camera.backgroundColor = Color.clear;
                utility.lights[0].intensity = 1.2f;
                utility.lights[0].transform.rotation = Quaternion.Euler(30f, 30f, 0f);
                utility.lights[1].intensity = 0.6f;

                runtimeMaterial = new Material(sourceMaterial) { hideFlags = HideFlags.HideAndDontSave };
                if (runtimeMaterial.HasProperty(MainTextureProperty))
                    runtimeMaterial.SetTexture(MainTextureProperty, source);
                if (runtimeMaterial.HasProperty(SpriteUVRectProperty))
                    runtimeMaterial.SetVector(
                        SpriteUVRectProperty,
                        sprite != null ? ESCompositeSpriteUVDriver.GetSpriteUVRect(sprite) : new Vector4(0f, 0f, 1f, 1f));
                bool hasSpriteTransform = ESCompositeSpriteUVDriver.TryGetSpriteUVTransform(
                    sprite,
                    out Vector4 spriteTransformX,
                    out Vector4 spriteTransformY);
                if (runtimeMaterial.HasProperty(SpriteUVTransformXProperty))
                    runtimeMaterial.SetVector(SpriteUVTransformXProperty, spriteTransformX);
                if (runtimeMaterial.HasProperty(SpriteUVTransformYProperty))
                    runtimeMaterial.SetVector(SpriteUVTransformYProperty, spriteTransformY);
                if (runtimeMaterial.HasProperty(SpriteUVTransformValidProperty))
                    runtimeMaterial.SetFloat(SpriteUVTransformValidProperty, hasSpriteTransform ? 1f : 0f);
                if (runtimeMaterial.HasProperty(TimeModeProperty))
                    runtimeMaterial.SetFloat(TimeModeProperty, (float)ESCompositeTimeMode.自定义时间);
                if (runtimeMaterial.HasProperty(CustomTimeProperty))
                    runtimeMaterial.SetFloat(CustomTimeProperty, time);
                ESCompositeShaderGUI.SyncMaterialKeywords(runtimeMaterial);

                mesh = CreateBakeQuad();
                Rect rect = new Rect(0f, 0f, size.x, size.y);
                utility.BeginPreview(rect, GUIStyle.none);
                utility.DrawMesh(mesh, Matrix4x4.identity, runtimeMaterial, 0);
                utility.camera.Render();
                Texture preview = utility.EndPreview();
                return CopyTexture(preview, size.x, size.y, useSrgb);
            }
            finally
            {
                if (mesh != null)
                    DestroyImmediate(mesh);
                if (runtimeMaterial != null)
                    DestroyImmediate(runtimeMaterial);
                if (utility != null)
                    utility.Cleanup();
            }
        }

        private static Mesh CreateBakeQuad()
        {
            var mesh = new Mesh { name = "ES Composite Bake Quad", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f)
            };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            mesh.tangents = new[]
            {
                new Vector4(1f, 0f, 0f, 1f),
                new Vector4(1f, 0f, 0f, 1f),
                new Vector4(1f, 0f, 0f, 1f),
                new Vector4(1f, 0f, 0f, 1f)
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Texture2D CopyTexture(Texture source, int width, int height, bool useSrgb)
        {
            if (source == null)
                return null;

            RenderTexture temporary = RenderTexture.GetTemporary(
                width,
                height,
                0,
                RenderTextureFormat.ARGB32,
                useSrgb ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;
                var result = new Texture2D(width, height, UnityEngine.TextureFormat.RGBA32, false, !useSrgb)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                result.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                result.Apply(false, false);
                return result;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
            }
        }

        private void PopulateFromSelection()
        {
            UnityEngine.Object selected = Selection.activeObject;
            Material selectedMaterial = selected as Material;
            if (selectedMaterial != null)
                material = selectedMaterial;

            PopulateSpriteFromSelection();
            ResolveTextureFromMaterial();
            ReleaseBakedTexture();
            lastError = null;
            Repaint();
        }

        private void PopulateSpriteFromSelection()
        {
            GameObject selectedObject = Selection.activeGameObject;
            if (selectedObject == null)
                return;

            SpriteRenderer spriteRenderer = selectedObject.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                if (IsSupportedMaterial(spriteRenderer.sharedMaterial))
                    material = spriteRenderer.sharedMaterial;
                sourceSprite = spriteRenderer.sprite;
                sourceTexture = sourceSprite != null ? sourceSprite.texture : null;
                return;
            }

            Image image = selectedObject.GetComponent<Image>();
            if (image != null)
            {
                if (IsSupportedMaterial(image.material))
                    material = image.material;
                sourceSprite = image.overrideSprite != null ? image.overrideSprite : image.sprite;
                sourceTexture = sourceSprite != null ? sourceSprite.texture : null;
            }
        }

        private void ResolveTextureFromMaterial()
        {
            if (sourceSprite != null)
            {
                sourceTexture = sourceSprite.texture;
                return;
            }
            if (material != null && material.HasProperty(MainTextureProperty))
                sourceTexture = material.GetTexture(MainTextureProperty) as Texture2D;
        }

        private Texture2D ResolveSourceTexture()
        {
            return sourceSprite != null ? sourceSprite.texture : sourceTexture;
        }

        private Vector2Int ResolveFrameSize()
        {
            if (sourceSprite != null)
                return new Vector2Int(
                    Mathf.Max(1, Mathf.RoundToInt(sourceSprite.rect.width)),
                    Mathf.Max(1, Mathf.RoundToInt(sourceSprite.rect.height)));

            Texture2D texture = ResolveSourceTexture();
            return texture != null ? new Vector2Int(texture.width, texture.height) : Vector2Int.one;
        }

        private void ReleaseBakedTexture()
        {
            if (bakedTexture != null)
                DestroyImmediate(bakedTexture);
            bakedTexture = null;
        }

        private static string SanitizeFileName(string value)
        {
            string result = string.IsNullOrWhiteSpace(value) ? "ESComposite" : value;
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++)
                result = result.Replace(invalid[i], '_');
            return result;
        }
    }
}
