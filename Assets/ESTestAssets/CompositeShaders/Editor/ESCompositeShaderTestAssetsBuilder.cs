using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ES.TestAssets.Editor
{
    /// <summary>
    /// Single authoring authority for the generated Composite Shader test assets.
    /// Nothing under this root references Assets/ESNormalAssets or third-party demo content.
    /// </summary>
    internal static partial class ESCompositeShaderTestAssetsBuilder
    {
        internal const string Root = "Assets/ESTestAssets/CompositeShaders";
        internal const string GeneratedRoot = Root + "/Generated";
        internal const string TextureRoot = GeneratedRoot + "/Textures";
        internal const string MaterialRoot = GeneratedRoot + "/Materials";
        internal const string SceneRoot = GeneratedRoot + "/Scenes";
        internal const string OverviewScenePath = SceneRoot + "/00_CompositeShader_TestOverview.unity";
        internal const string AuthoredUiUxmlPath = Root + "/UI/ESCompositeShaderObservationPanel.uxml";
        internal const string AuthoredUiUssPath = Root + "/UI/ESCompositeShaderObservationPanel.uss";

        internal const string Shader2D = "ES/2D/Composite URP";
        internal const string ShaderUI = "ES/UI/Composite URP";
        internal const string ShaderLit = "ES/3D/Lit Composite URP";
        internal const string ShaderVfx = "ES/3D/VFX Composite URP";

        private const string BuildMenu = "【ES】/验证与诊断/验证环境/Shader/创建或刷新 Composite Shader 独立测试资产 _F12";
        private const string OpenMenu = "【ES】/验证与诊断/验证环境/Shader/打开 Composite Shader 测试总览";

        internal sealed class GeneratedTextures
        {
            public Texture2D Icon;
            public Texture2D Noise;
            public Texture2D Flow;
            public Texture2D Sequence;
            public Sprite IconSprite;
        }

        internal sealed class GeneratedMaterials
        {
            private readonly Dictionary<string, Material> values = new Dictionary<string, Material>(StringComparer.Ordinal);

            public void Add(string id, Material material) => values.Add(id, material);

            public Material Get(string id)
            {
                if (!values.TryGetValue(id, out Material material) || material == null)
                    throw new InvalidOperationException("Composite Shader 测试材质不存在：" + id);
                return material;
            }
        }

        [MenuItem(BuildMenu, false, 140)]
        public static void CreateOrRefreshAll()
        {
            EnsureEditorReady();
            EnsureFolders();

            try
            {
                GeneratedTextures textures = CreateTextures();
                GeneratedMaterials materials = CreateMaterials(textures);
                NormalizeGeneratedMaterialNames();
                // Persist repaired main-object identities before any scene serializes
                // references to the generated materials. Unity validates these names
                // during SaveScene and otherwise repeats the warning for every scene.
                AssetDatabase.SaveAssets();
                CreateScenes(textures, materials);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

                SceneAsset overview = AssetDatabase.LoadAssetAtPath<SceneAsset>(OverviewScenePath);
                if (overview == null)
                    throw new InvalidOperationException("生成结束后无法重新加载测试总览场景：" + OverviewScenePath);

                Selection.activeObject = overview;
                EditorGUIUtility.PingObject(overview);
                Debug.Log("[ESTestAssets] Composite Shader 独立测试资产已生成，共 6 个场景、57 个材质案例。", overview);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                throw;
            }
        }

        [MenuItem(OpenMenu, false, 141)]
        public static void OpenOverview()
        {
            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(OverviewScenePath);
            if (sceneAsset == null)
            {
                EditorUtility.DisplayDialog(
                    "Composite Shader 测试资产尚未生成",
                    "请先执行：\n" + BuildMenu.Replace(" _F12", string.Empty),
                    "知道了");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EditorSceneManager.OpenScene(OverviewScenePath, OpenSceneMode.Single);
            Selection.activeObject = sceneAsset;
            EditorGUIUtility.PingObject(sceneAsset);
        }

        private static void EnsureEditorReady()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("只能在 EditMode 生成 Composite Shader 测试资产。");
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Unity 正在编译或导入，请等待完成后重试。");

            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && string.IsNullOrEmpty(activeScene.path) && activeScene.rootCount > 0)
                throw new InvalidOperationException("当前存在未保存场景内容，请先保存或关闭，再生成测试场景。");
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/ESTestAssets");
            EnsureFolder(Root);
            EnsureFolder(GeneratedRoot);
            EnsureFolder(TextureRoot);
            EnsureFolder(MaterialRoot);
            EnsureFolder(MaterialRoot + "/01_2D");
            EnsureFolder(MaterialRoot + "/02_UI");
            EnsureFolder(MaterialRoot + "/03_3D_Lit");
            EnsureFolder(MaterialRoot + "/04_3D_VFX");
            EnsureFolder(MaterialRoot + "/05_ProductionRecipes");
            EnsureFolder(MaterialRoot + "/90_Environment");
            EnsureFolder(SceneRoot);
        }


        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
                return;

            string parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            string name = Path.GetFileName(assetPath);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
                throw new InvalidOperationException("无效的 Unity 资产目录：" + assetPath);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        internal static Material CreateOrResetMaterial(string path, string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
                throw new InvalidOperationException("找不到 Shader：" + shaderName);

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                var defaults = new Material(shader) { name = material.name };
                EditorUtility.CopySerialized(defaults, material);
                UnityEngine.Object.DestroyImmediate(defaults);
            }

            // The asset filename is the authoritative stable identity for generated test materials.
            // Keep the serialized Unity object name aligned after any prior file rename; otherwise
            // EditorSceneManager.SaveScene can reject scene serialization with a filename mismatch.
            string expectedName = Path.GetFileNameWithoutExtension(path);
            if (!string.Equals(material.name, expectedName, StringComparison.Ordinal))
                material.name = expectedName;

            material.shader = shader;
            return material;
        }

        private static void NormalizeGeneratedMaterialNames()
        {
            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { MaterialRoot });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                    continue;

                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                    continue;

                string expectedName = Path.GetFileNameWithoutExtension(path);
                if (string.Equals(material.name, expectedName, StringComparison.Ordinal))
                    continue;

                material.name = expectedName;
                EditorUtility.SetDirty(material);
            }
        }

        internal static void Set(Material material, string propertyName, float value)
        {
            RequireProperty(material, propertyName);
            material.SetFloat(propertyName, value);
        }

        internal static void Set(Material material, string propertyName, Color value)
        {
            RequireProperty(material, propertyName);
            material.SetColor(propertyName, value);
        }

        internal static void Set(Material material, string propertyName, Vector4 value)
        {
            RequireProperty(material, propertyName);
            material.SetVector(propertyName, value);
        }

        internal static void Set(Material material, string propertyName, Texture value)
        {
            RequireProperty(material, propertyName);
            material.SetTexture(propertyName, value);
        }

        internal static void SetIfPresent(Material material, string propertyName, Texture value)
        {
            if (material.HasProperty(propertyName))
                material.SetTexture(propertyName, value);
        }

        private static void RequireProperty(Material material, string propertyName)
        {
            if (material == null || !material.HasProperty(propertyName))
                throw new MissingMemberException(material == null ? "<null material>" : material.shader.name, propertyName);
        }

        internal static Color Hdr(float r, float g, float b, float a = 1f) => new Color(r, g, b, a);
    }
}
