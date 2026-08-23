using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ES.TestAssets.Editor
{
    internal static partial class ESCompositeShaderTestAssetsBuilder
    {
        private static string backupRunDirectory;

        private static void CreateScenes(GeneratedTextures textures, GeneratedMaterials materials)
        {
            backupRunDirectory = null;
            CreateOverviewScene(textures, materials);
            Create2DScene(textures, materials);
            CreateUIScene(textures, materials);
            CreateLitScene(materials);
            CreateVfxScene(materials);
            CreateProductionScene(textures, materials);
        }

        private static void CreateRuntimeTool(
            Transform root,
            string title,
            string category,
            string focus,
            ESCompositeShaderTestAnimator animator)
        {
            if (animator == null)
                throw new InvalidOperationException("运行时测试工具缺少 ESCompositeShaderTestAnimator：" + title);
            animator.ConfigureRuntimeTool(title, category, focus, string.Equals(category, "Overview", System.StringComparison.OrdinalIgnoreCase));
        }

        private static void CreateSceneHeader(
            Transform root,
            string title,
            string subtitle,
            string focus,
            float y,
            Color accent)
        {
            CreateWorldLabel(root, "◆ " + title, new Vector3(0f, y, 0f), 0.115f, Color.white);
            CreateWorldLabel(root, subtitle, new Vector3(0f, y - 0.62f, 0f), 0.058f, accent);
            CreateWorldLabel(root, "验证重点 · " + focus, new Vector3(0f, y - 1.06f, 0f), 0.042f, new Color(0.72f, 0.78f, 0.88f, 1f));
        }

        private static string FormatCaseLabel(string title, Material material)
        {
            string materialName = material == null ? "<材质缺失>" : material.name;
            return title + "\n材质 · " + materialName;
        }

        private static void BuildScene(string path, string sceneName, Action<Transform> build)
        {
            Scene previousActive = SceneManager.GetActiveScene();
            bool useSingle = Application.isBatchMode
                             || (previousActive.IsValid()
                                 && string.IsNullOrEmpty(previousActive.path)
                                 && previousActive.rootCount == 0);
            NewSceneMode mode = useSingle ? NewSceneMode.Single : NewSceneMode.Additive;
            BackupExistingGeneratedScene(path);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
            try
            {
                if (mode == NewSceneMode.Additive && SceneManager.GetActiveScene() != scene)
                {
                    SceneManager.SetActiveScene(scene);
                    if (SceneManager.GetActiveScene() != scene)
                        throw new InvalidOperationException("无法激活待生成场景：" + sceneName);
                }

                // Unity serializes the scene's main/root object identity and validates it
                // against the asset filename. Keep that stable identity file-based while
                // the authored UI/header continues to use the human-facing sceneName.
                string sceneAssetName = Path.GetFileNameWithoutExtension(path);
                scene.name = sceneAssetName;
                Transform root = new GameObject(sceneAssetName).transform;
                build(root);
                ESCompositeShaderTestAnimator runtimeTool = root.GetComponent<ESCompositeShaderTestAnimator>();
                if (runtimeTool != null)
                {
                    runtimeTool.ConfigureSceneNavigation(
                        path,
                        OverviewScenePath,
                        new[]
                        {
                            OverviewScenePath,
                            SceneRoot + "/01_CompositeShader_2D_Cases.unity",
                            SceneRoot + "/02_CompositeShader_UI_Cases.unity",
                            SceneRoot + "/03_CompositeShader_3D_Lit_Cases.unity",
                            SceneRoot + "/04_CompositeShader_3D_VFX_Cases.unity",
                            SceneRoot + "/05_CompositeShader_ProductionRecipes.unity",
                        },
                        new[] { "总览", "2D", "UI", "Lit", "VFX", "配方" });
                }
                ValidateGeneratedScene(scene, path);

                if (!EditorSceneManager.SaveScene(scene, path))
                    throw new InvalidOperationException("保存测试场景失败：" + path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                    throw new InvalidOperationException("保存后无法重新加载测试场景：" + path);
            }
            finally
            {
                if (mode == NewSceneMode.Additive)
                {
                    if (previousActive.IsValid() && previousActive.isLoaded)
                        SceneManager.SetActiveScene(previousActive);
                    if (scene.IsValid() && scene.isLoaded)
                        EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void BackupExistingGeneratedScene(string scenePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                                 ?? throw new InvalidOperationException("无法解析 Unity 项目根目录。");
            string sourcePath = Path.Combine(projectRoot, scenePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(sourcePath))
                return;

            if (string.IsNullOrEmpty(backupRunDirectory))
            {
                string runId = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture);
                backupRunDirectory = Path.Combine(
                    projectRoot,
                    "ES",
                    "Bak",
                    "Local",
                    "CompositeShaderTestAssets",
                    runId);
                Directory.CreateDirectory(Path.Combine(backupRunDirectory, "Scenes"));
                File.WriteAllText(
                    Path.Combine(backupRunDirectory, "BACKUP_MANIFEST.md"),
                    "# Composite Shader Test Assets Before Backup\n\n"
                    + "| Source | UTC | Bytes | SHA-256 | Backup |\n"
                    + "|---|---|---:|---|---|\n",
                    new UTF8Encoding(false));
            }

            string backupRelativePath = Path.Combine("Scenes", Path.GetFileName(scenePath));
            string backupPath = Path.Combine(backupRunDirectory, backupRelativePath);
            var sourceInfo = new FileInfo(sourcePath);
            string hash = ComputeSha256(sourcePath);
            File.Copy(sourcePath, backupPath, true);
            File.AppendAllText(
                Path.Combine(backupRunDirectory, "BACKUP_MANIFEST.md"),
                string.Format(
                    CultureInfo.InvariantCulture,
                    "| `{0}` | `{1:O}` | {2} | `{3}` | `{4}` |\n",
                    scenePath,
                    DateTime.UtcNow,
                    sourceInfo.Length,
                    hash,
                    backupRelativePath.Replace('\\', '/')),
                new UTF8Encoding(false));
        }

        private static string ComputeSha256(string path)
        {
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] hash = sha256.ComputeHash(stream);
                var builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }

        private static void ValidateGeneratedScene(Scene scene, string path)
        {
            if (!scene.IsValid() || !scene.isLoaded || scene.rootCount != 1)
                throw new InvalidOperationException("测试场景根结构无效：" + path);

            GameObject[] roots = scene.GetRootGameObjects();
            string expectedRootName = Path.GetFileNameWithoutExtension(path);
            if (roots.Length != 1 || !string.Equals(roots[0].name, expectedRootName, StringComparison.Ordinal))
                throw new InvalidOperationException("测试场景根对象必须与文件名一致：" + path + " / " + roots[0].name);
            Camera[] cameras = roots[0].GetComponentsInChildren<Camera>(true);
            if (cameras.Length == 0)
                throw new InvalidOperationException("测试场景缺少 Camera：" + path);
            if (roots[0].GetComponentsInChildren<ESSceneValidationGuide>(true).Length != 1)
                throw new InvalidOperationException("测试场景必须恰好包含一个 ESSceneValidationGuide：" + path);

            Component[] components = roots[0].GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                    throw new InvalidOperationException("测试场景包含 Missing Script：" + path);
            }
        }

        private static Camera CreateOrthographicCamera(Transform root, float size, Color background)
        {
            GameObject cameraObject = new GameObject("Test Camera");
            cameraObject.transform.SetParent(root, false);
            cameraObject.transform.position = new Vector3(0f, 0f, -20f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = size;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.depthTextureMode |= DepthTextureMode.Depth;
            return camera;
        }

        private static Camera CreatePerspectiveCamera(Transform root, Vector3 position, Vector3 lookAt)
        {
            GameObject cameraObject = new GameObject("Test Camera");
            cameraObject.transform.SetParent(root, false);
            cameraObject.transform.position = position;
            cameraObject.transform.rotation = Quaternion.LookRotation(lookAt - position, Vector3.up);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 52f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.032f, 0.046f, 1f);
            camera.depthTextureMode |= DepthTextureMode.Depth;
            UniversalAdditionalCameraData additionalCameraData =
                cameraObject.GetComponent<UniversalAdditionalCameraData>()
                ?? cameraObject.AddComponent<UniversalAdditionalCameraData>();
            additionalCameraData.requiresDepthTexture = true;
            return camera;
        }

        private static void CreateLighting(Transform root)
        {
            GameObject key = new GameObject("Key Directional Light");
            key.transform.SetParent(root, false);
            key.transform.rotation = Quaternion.Euler(42f, -32f, 0f);
            Light keyLight = key.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = new Color(1f, 0.94f, 0.84f, 1f);
            keyLight.intensity = 1.25f;
            keyLight.shadows = LightShadows.Soft;

            GameObject fill = new GameObject("Fill Light");
            fill.transform.SetParent(root, false);
            fill.transform.position = new Vector3(-8f, 5f, -4f);
            Light fillLight = fill.AddComponent<Light>();
            fillLight.type = LightType.Point;
            fillLight.color = new Color(0.2f, 0.55f, 1f, 1f);
            fillLight.range = 24f;
            fillLight.intensity = 4f;
        }

        private static SpriteRenderer CreateSpriteCase(
            Transform root,
            string title,
            Vector3 position,
            Sprite sprite,
            Material material,
            float scale = 1.6f,
            float labelOffset = 1.55f,
            float labelCharacterSize = 0.042f)
        {
            GameObject caseRoot = new GameObject("Case · " + title);
            caseRoot.transform.SetParent(root, false);
            caseRoot.transform.localPosition = position;
            GameObject instance = new GameObject(title);
            instance.transform.SetParent(caseRoot.transform, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localScale = Vector3.one * scale;
            SpriteRenderer renderer = instance.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            CreateWorldLabel(caseRoot.transform, title, new Vector3(0f, labelOffset, 0f), 0.072f);
            CreateWorldLabel(caseRoot.transform, "材质 · " + (material == null ? "<材质缺失>" : material.name), new Vector3(0f, labelOffset - 0.34f, 0f), labelCharacterSize, new Color(0.64f, 0.72f, 0.84f, 1f));
            return renderer;
        }

        private static Renderer CreatePrimitiveCase(
            Transform root,
            string title,
            PrimitiveType primitive,
            Vector3 position,
            Material material,
            Vector3 scale,
            float labelOffset = 0.78f,
            float labelCharacterSize = 0.048f)
        {
            GameObject caseRoot = new GameObject("Case · " + title);
            caseRoot.transform.SetParent(root, false);
            caseRoot.transform.localPosition = position;
            GameObject instance = GameObject.CreatePrimitive(primitive);
            instance.name = title;
            instance.transform.SetParent(caseRoot.transform, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localScale = scale;
            Collider collider = instance.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
            Renderer renderer = instance.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            float labelY = scale.y * 0.72f + labelOffset;
            CreateWorldLabel(caseRoot.transform, title, new Vector3(0f, labelY, 0f), 0.072f);
            CreateWorldLabel(caseRoot.transform, "材质 · " + (material == null ? "<材质缺失>" : material.name), new Vector3(0f, labelY - 0.32f, 0f), labelCharacterSize, new Color(0.64f, 0.72f, 0.84f, 1f));
            return renderer;
        }

        private static TextMesh CreateWorldLabel(
            Transform root,
            string text,
            Vector3 position,
            float characterSize,
            Color? color = null)
        {
            GameObject labelObject = new GameObject(text + " Label");
            labelObject.transform.SetParent(root, false);
            labelObject.transform.localPosition = position;
            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 48;
            int visibleLength = string.IsNullOrEmpty(text) ? 1 : text.Replace("\n", string.Empty).Length;
            float fit = Mathf.Clamp(32f / visibleLength, 0.68f, 1f);
            label.characterSize = characterSize * fit;
            label.color = color ?? new Color(0.82f, 0.88f, 0.96f, 1f);
            label.richText = false;
            return label;
        }

        private static ESSceneValidationGuide CreateGuide(
            Transform root,
            Camera camera,
            string title,
            string subtitle,
            IList<Transform> landmarks,
            bool showOverlay = true)
        {
            GameObject diagnostics = new GameObject("Diagnostics");
            diagnostics.transform.SetParent(root, false);
            ESSceneValidationGuide guide = diagnostics.AddComponent<ESSceneValidationGuide>();
            guide.worldGuideCamera = camera;
            guide.autoSelectNearestStage = false;
            guide.showRuntimeOverlay = showOverlay;

            var stages = new List<ESSceneValidationStage>();
            for (int i = 0; i < landmarks.Count; i++)
            {
                stages.Add(new ESSceneValidationStage
                {
                    id = "visual-group-" + (i + 1),
                    title = "视觉组 " + (i + 1),
                    landmark = landmarks[i],
                    objective = "观察当前组中各材质的方向、遮罩、边缘与动态效果。",
                    expectedResult = "每个案例与标签一致，关闭项不改变结果，动画方向与材质名称一致。",
                    failureHint = "先检查材质质量档、父开关、纹理引用、Camera Depth Texture 与对象几何边界。",
                    checkIds = new[] { "manual-visual" },
                });
            }

            var checks = new List<ESSceneValidationCheck>
            {
                new ESSceneValidationCheck
                {
                    id = "manual-visual",
                    title = "Composite Shader 视觉矩阵",
                    kind = ESSceneValidationCheckKind.ManualObservation,
                    manualHint = "进入 PlayMode 后逐项观察；人工观察不冒充自动通过。",
                },
            };
            guide.ConfigureForAuthoring(title, subtitle, stages, checks);
            return guide;
        }

        private static void CreateFloor(Transform root, Material material, Vector3 position, Vector3 scale)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Test Floor";
            floor.transform.SetParent(root, false);
            floor.transform.localPosition = position;
            floor.transform.localScale = scale;
            floor.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = floor.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
        }
    }
}
