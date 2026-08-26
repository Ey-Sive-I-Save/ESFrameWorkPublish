#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.TextCore.LowLevel;
using TMPro;
using ES.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ES.Editor
{
    // UGUI materialization keeps editor/runtime assembly boundaries explicit.
    /// <summary>
    /// Deterministic materializer for AI-authored visual UI specs. It creates a Prefab and an isolated fixture scene.
    /// The spec is the reviewable boundary; this window never invents business data or runtime window ownership.
    /// </summary>
    internal static class ESUIGameScreenMaterializer
    {
        private const string ShowcaseSpecPath = "Assets/UI/Contracts/ESCompositeShaderShowcase.screen-spec.v3.json";
        private const string MaterializerContractPath = ".agents/skills/es-ui-prefab-authoring/references/game-ui-materializer-contract.md";
        private const string ShowcaseFontPath = "Assets/UI/Fonts/ESBrandSansSC SDF.asset";
        private const string ShowcaseFontMaterialPath = "Assets/UI/Fonts/ESBrandSansSC SDF.mat";
        private const string MaterializerBuildId = "ai-ui-visual-assets-v7";
        private static TMP_FontAsset ActiveFontAsset;
        private static Material ActiveFontMaterial;

        [MenuItem("【ES】/验证与诊断/验证环境/UI/材质化 Composite Shader Showcase UI", false, 142)]
        public static void MaterializeCompositeShaderShowcaseInCurrentEditor()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                                 ?? throw new InvalidOperationException("无法解析 Unity 项目根目录。");
            string specAbsolutePath = Path.Combine(projectRoot, ShowcaseSpecPath.Replace('/', Path.DirectorySeparatorChar));
            string contractAbsolutePath = Path.Combine(projectRoot, MaterializerContractPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(specAbsolutePath)) throw new FileNotFoundException("ScreenSpec 不存在。", ShowcaseSpecPath);
            if (!File.Exists(contractAbsolutePath)) throw new FileNotFoundException("材质化合同不存在。", MaterializerContractPath);

            string runId = "interactive-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");
            string evidenceRoot = "ES/UIEvidence/es-composite-shader-showcase/" + runId;
            string specJson = File.ReadAllText(specAbsolutePath, new UTF8Encoding(false, true));
            string specHash = ComputeSha256(specAbsolutePath);
            string contractHash = ComputeSha256(contractAbsolutePath);
            string result = ExecuteAuthoringJsonCore(
                specJson,
                new[] { "wide", "narrow" },
                new[] { "default", "selected", "disabled", "loading", "error", "long-content" },
                false,
                contractHash,
                runId,
                1,
                evidenceRoot,
                specHash);
            string resultPath = evidenceRoot + "/interactive-materialization-result.json";
            ESManagedFileIO.WriteTextAtomic(
                ESAutomationPathPolicy.Normalize(resultPath),
                result,
                new UTF8Encoding(false),
                ESAutomationPathPolicy.Normalize(evidenceRoot));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ESUIGameScreenMaterializer] 当前 Unity 编辑器进程内材质化完成：" + resultPath);
        }

        [MenuItem("【ES】/验证与诊断/验证环境/UI/仅刷新 Composite Shader Showcase UGUI Prefab", false, 143)]
        public static void RefreshCompositeShaderShowcasePrefabOnly()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                                 ?? throw new InvalidOperationException("无法解析 Unity 项目根目录。");
            string specPath = Path.Combine(projectRoot, ShowcaseSpecPath.Replace('/', Path.DirectorySeparatorChar));
            string specJson = ESUIScreenSpecAdapter.Normalize(File.ReadAllText(specPath, new UTF8Encoding(false, true)));
            RejectUnknownUiSpecFields(JObject.Parse(specJson), "$spec");
            UiSpec spec = JsonConvert.DeserializeObject<UiSpec>(specJson);
            if (spec == null || string.IsNullOrWhiteSpace(spec.panelId))
                throw new InvalidDataException("ScreenSpec panelId 缺失。");
            ValidateSpec(spec, new[] { "wide", "narrow" });
            EnsureParentFolder(spec.prefabPath);
            GameObject root = BuildRoot(spec);
            try
            {
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, spec.prefabPath);
                if (prefab == null)
                    throw new InvalidOperationException("UGUI Prefab 保存失败：" + spec.prefabPath);
                AssetDatabase.SaveAssetIfDirty(prefab);
                Debug.Log("[ESUIGameScreenMaterializer] Authored UGUI Prefab refreshed: " + spec.prefabPath);
            }
            finally
            {
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [MenuItem("【ES】/验证与诊断/验证环境/UI/重建 Composite Shader Showcase 字体", false, 144)]
        public static void RebuildCompositeShaderShowcaseFont()
        {
            const string sourcePath = "Assets/Plugins/ES/Editor/Resources/ESPresentation/Fonts/ESBrandSansSC.otf";
            Font source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (source == null)
                throw new FileNotFoundException("ES 展示字体源文件不存在。", sourcePath);
            Shader shader = Shader.Find("TextMeshPro/Distance Field");
            if (shader == null)
                throw new InvalidOperationException("TextMeshPro/Distance Field Shader 不可用。");

            TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(
                source, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048,
                AtlasPopulationMode.Dynamic, true);
            if (font == null)
                throw new InvalidOperationException("TMP 字体资产创建失败。");
            string seed = "_ES 独立 Shader 漫游展示 重置 应用 预设 加载 错误 禁用 选择 自动动画 单项 全部概览 2D UI Lit VFX 强度速度功能参数";
            font.TryAddCharacters(seed, out string missingCharacters);
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            font.ReadFontAssetDefinition();

            EnsureParentFolder(ShowcaseFontPath);
            string swapToken = Guid.NewGuid().ToString("N");
            string temporaryFontPath = BuildSwapAssetPath(ShowcaseFontPath, "tmp", swapToken);
            string temporaryMaterialPath = BuildSwapAssetPath(ShowcaseFontMaterialPath, "tmp", swapToken);
            string backupFontPath = BuildSwapAssetPath(ShowcaseFontPath, "bak", swapToken);
            string backupMaterialPath = BuildSwapAssetPath(ShowcaseFontMaterialPath, "bak", swapToken);
            Material material = null;
            bool fontAssetCreated = false;
            bool materialAssetCreated = false;
            bool fontBackedUp = false;
            bool materialBackedUp = false;
            bool fontSwapped = false;
            bool materialSwapped = false;
            try
            {
                AssetDatabase.CreateAsset(font, temporaryFontPath);
                fontAssetCreated = true;
                if (font.atlasTexture != null)
                {
                    font.atlasTexture.name = "ESBrandSansSC SDF Atlas";
                    AssetDatabase.AddObjectToAsset(font.atlasTexture, font);
                }

                material = new Material(shader) { name = "ESBrandSansSC SDF" };
                material.SetTexture(ShaderUtilities.ID_MainTex, font.atlasTexture);
                AssetDatabase.CreateAsset(material, temporaryMaterialPath);
                materialAssetCreated = true;
                font.material = material;
                EditorUtility.SetDirty(font);
                AssetDatabase.SaveAssetIfDirty(font);
                AssetDatabase.SaveAssetIfDirty(material);

                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(ShowcaseFontPath) != null)
                {
                    string backupError = AssetDatabase.MoveAsset(ShowcaseFontPath, backupFontPath);
                    if (!string.IsNullOrEmpty(backupError)) throw new InvalidOperationException(backupError);
                    fontBackedUp = true;
                }
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(ShowcaseFontMaterialPath) != null)
                {
                    string backupError = AssetDatabase.MoveAsset(ShowcaseFontMaterialPath, backupMaterialPath);
                    if (!string.IsNullOrEmpty(backupError)) throw new InvalidOperationException(backupError);
                    materialBackedUp = true;
                }
                string fontSwapError = AssetDatabase.MoveAsset(temporaryFontPath, ShowcaseFontPath);
                if (!string.IsNullOrEmpty(fontSwapError)) throw new InvalidOperationException(fontSwapError);
                fontSwapped = true;
                string materialSwapError = AssetDatabase.MoveAsset(temporaryMaterialPath, ShowcaseFontMaterialPath);
                if (!string.IsNullOrEmpty(materialSwapError)) throw new InvalidOperationException(materialSwapError);
                materialSwapped = true;
                if (fontBackedUp && !AssetDatabase.DeleteAsset(backupFontPath))
                    Debug.LogWarning("Composite Shader 字体重建已完成，但旧字体备份未能清理，请手动检查：" + backupFontPath);
                if (materialBackedUp && !AssetDatabase.DeleteAsset(backupMaterialPath))
                    Debug.LogWarning("Composite Shader 字体重建已完成，但旧材质备份未能清理，请手动检查：" + backupMaterialPath);
                AssetDatabase.Refresh();
                Debug.Log("[ESUIGameScreenMaterializer] Composite Shader 字体已重建：glyphs=" + (font.glyphTable?.Count ?? 0) + ", missing=" + (missingCharacters ?? string.Empty));
            }
            catch (Exception exception)
            {
                if (materialSwapped && AssetDatabase.LoadMainAssetAtPath(ShowcaseFontMaterialPath) == material)
                    AssetDatabase.DeleteAsset(ShowcaseFontMaterialPath);
                if (materialAssetCreated && AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(temporaryMaterialPath) != null)
                    AssetDatabase.DeleteAsset(temporaryMaterialPath);
                else if (material != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(material)))
                    UnityEngine.Object.DestroyImmediate(material);
                if (fontSwapped && AssetDatabase.LoadMainAssetAtPath(ShowcaseFontPath) == font)
                    AssetDatabase.DeleteAsset(ShowcaseFontPath);
                if (fontAssetCreated && AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(temporaryFontPath) != null)
                    AssetDatabase.DeleteAsset(temporaryFontPath);
                else if (font != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(font)))
                    UnityEngine.Object.DestroyImmediate(font, true);
                if (fontBackedUp && AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(ShowcaseFontPath) == null)
                {
                    string restoreError = AssetDatabase.MoveAsset(backupFontPath, ShowcaseFontPath);
                    if (!string.IsNullOrEmpty(restoreError))
                        Debug.LogError("Composite Shader 字体旧资产恢复失败，备份仍保留：" + backupFontPath + "；" + restoreError);
                }
                if (materialBackedUp && AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(ShowcaseFontMaterialPath) == null)
                {
                    string restoreError = AssetDatabase.MoveAsset(backupMaterialPath, ShowcaseFontMaterialPath);
                    if (!string.IsNullOrEmpty(restoreError))
                        Debug.LogError("Composite Shader 材质旧资产恢复失败，备份仍保留：" + backupMaterialPath + "；" + restoreError);
                }
                Debug.LogException(new InvalidOperationException("Composite Shader 字体重建失败，已回滚本次生成。", exception));
                throw;
            }
        }

        private static string BuildSwapAssetPath(string assetPath, string kind, string token)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                throw new ArgumentException("交换资产路径不能为空。", nameof(assetPath));
            int extensionIndex = assetPath.LastIndexOf(".asset", StringComparison.OrdinalIgnoreCase);
            if (extensionIndex < 0)
                throw new ArgumentException("交换资产必须使用 .asset 扩展名。", nameof(assetPath));
            return assetPath.Substring(0, extensionIndex) + "." + kind + "-" + token + ".asset";
        }

        [MenuItem("【ES】/验证与诊断/验证环境/UI/打开 Composite Shader UI 案例场景", false, 145)]
        public static void OpenCompositeShaderUICasesScene()
        {
            const string scenePath = "Assets/ESTestAssets/CompositeShaders/Generated/Scenes/02_CompositeShader_UI_Cases.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                throw new FileNotFoundException("Composite Shader UI 案例场景不存在。", scenePath);
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Debug.Log("[ESUIGameScreenMaterializer] 已进入 Composite Shader UI 案例场景：" + scenePath);
        }

        private static string ComputeSha256(string absolutePath)
        {
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = File.OpenRead(absolutePath))
            {
                byte[] hash = sha256.ComputeHash(stream);
                StringBuilder builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) builder.Append(hash[i].ToString("x2"));
                return builder.ToString();
            }
        }

        private static readonly HashSet<string> UiSpecFieldNames = new HashSet<string>(new[]
        {
            "panelId", "prefabPath", "fixtureScenePath", "referenceImagePath", "narrowAspectThreshold", "tokens", "assets", "profiles", "states", "qualityGates", "designContract", "intentContract", "stateSemantics", "profileAvailability", "bindings", "artifactStatus", "generationMode", "designEvidence", "rootLayoutIntent", "elements",
            "background", "surface", "surfaceRaised", "accent", "accentWarm", "onAccent", "text", "mutedText", "danger", "onDanger", "titleSize", "bodySize", "buttonSize", "labelSize", "captionSize", "numericSize", "spacing", "padding",
            "schemaVersion", "sourceType", "brief", "analysisArtifact", "visionReview", "provider", "model", "reviewMethod", "reviewedAt", "imageHashes", "semanticCoverage", "method", "sha256", "referenceImages", "sourceRegions", "decisions", "responsiveDecisions", "assetDecisions", "assumptions",
            "path", "role", "source", "fallback", "status", "id", "fixture", "width", "height", "orientation", "safeArea", "bounds", "evidence", "confidence", "major", "elementId", "sourceRegionId", "layoutMode", "tokenRoles", "hash", "provenance", "license", "importPolicy", "aspectPolicy", "sourceAspectRatio", "focalPoint", "cropPolicy", "nineSlice", "atlasOwner", "atlasRotationPolicy", "resolutionSet",
            "geometry", "visual", "interaction", "anchorStrategy", "anchorEdge", "safeArea", "sizeStrategy", "pivot", "rationale", "typographyRole", "colorRoles", "spacingRole", "layerRole", "siblingOrder",
            "coordinateSpace", "canvas", "rootRole", "renderMode", "scalerMode", "singleRoot", "nestedCanvasPolicy", "anchorPolicy", "defaultPivot", "allowedStrategies", "safeAreaTarget", "layerRoles", "primaryAction", "secondaryAction", "feedback", "foregroundOnAccent", "foregroundOnDanger", "typographyRoles", "title", "body", "label", "caption", "numeric", "token", "maxLines", "overflow", "role",
            "alignment", "assetRole", "raycastPolicy", "minTarget", "profileId", "strategy", "layoutPolicy", "changes", "reason", "statement",
            "kind", "componentType", "visualVariant", "assetSlots", "value", "hasValue", "elementText", "colorToken", "layout", "width", "height", "minWidth", "minHeight", "maxWidth", "fillWidth", "interactable", "layoutIntent", "children", "layoutSpec",
            "layoutMode", "axis", "gap", "paddingLeft", "paddingRight", "paddingTop", "paddingBottom", "columns", "cellWidth", "cellHeight", "spacingX", "spacingY",
            "cellSize", "spacing", "childAlignment", "controlChildWidth", "controlChildHeight", "forceChildExpandWidth", "forceChildExpandHeight",
            "narrowLayoutIntent", "wrapText", "maxLines", "overflow", "mode", "anchorMinX", "anchorMinY", "anchorMaxX", "anchorMaxY", "pivotX",
            "pivotY", "anchoredX", "anchoredY", "sizeWidth", "sizeHeight", "ignoreParentLayout", "text",
            "assetPolicy", "productionReady", "commercialAcceptance", "acceptedSources", "requiredFields", "plannedSourceClasses", "placeholderUse",
            "responsivePolicy", "canvasRenderMode", "canvasScalerMode", "referenceResolution", "matchWidthOrHeight", "safeAreaPolicy", "reflowPolicy", "longContentPolicy", "uniformScaleOnly", "profileIds",
            "colorPolicy", "minimumTextContrast", "nonColorStateSignals", "typographyPolicy", "fontAssetId", "fontAssetPath", "fontAssetHash", "fallbackFontAssetIds", "fallbackFontAssets", "fallbackFontAssetId", "fallbackFontAssetPath", "fallbackFontAssetHash", "fallbackFontAssetLicense", "requiredCharacters", "localeFixtures", "overflowPolicy", "advancedComposition", "primaryActions", "logicalId", "componentIdsByProfile", "focalTreatment", "noFocalReason", "focalSubjects", "protectedFromPrimaryAction", "focalAssetPolicies", "assetIds", "safeCropInsetsNormalized", "alignmentGroups", "axis", "edge", "componentIds", "tolerancePx", "clearanceConstraints", "relation", "firstComponentId", "secondComponentId", "minGapPx", "responsiveEquivalences", "interactionDensity", "groups", "maxTargets",
            "requestedScreenFamily", "requestedPrimaryIntent", "visualTarget", "fidelityMode", "referencePolicy", "referenceSources", "productBoundary", "fixtureData", "fixtureTextBindings", "fixtureDataKey", "overflowPolicy", "contentInsetsPx", "reserveActionClearancePx", "affectedComponentIds", "visualChanges", "interactionChanges", "geometryPolicy", "preserveBounds", "allowedChanges", "effects", "componentId", "changes", "visible", "graphicAlpha", "graphicColor", "outline", "ruleId", "componentIds", "profileIds", "stateIds", "evidenceRequirements", "nextArtifactFields", "priorEvidenceBatch", "ruleIds", "changedFields", "expectedEffects", "falsificationChecks", "evidenceLedger", "static", "materialization", "gpuVisual", "runtime", "runtimeEvidence", "availableIntents", "omittedIntents", "targetSize", "intent", "stateVariants", "interaction", "decisionId", "decision", "layoutDecisions", "conflictHypotheses", "artifactStatus", "assets", "prefab", "fixtureScene", "gpuEvidence", "fixtureScene", "generationMode", "wide", "narrow", "default", "selected", "disabled", "loading", "error", "long-content", "action", "information", "focus", "childGeometryOwner"
        }, StringComparer.Ordinal);

        [Serializable] private sealed class UiSpec
        {
            public string panelId = "ui-panel";
            public string prefabPath = "Assets/UI/Prefabs/Generated/ui-panel.prefab";
            public string fixtureScenePath = "Assets/UI/Scenes/Generated/ui-panel-fixture.unity";
            public string referenceImagePath = string.Empty;
            public float narrowAspectThreshold = 1.15f;
            public UiTokens tokens = new UiTokens();
            public UiAsset[] assets = Array.Empty<UiAsset>();
            public JArray profiles = new JArray();
            public JArray states = new JArray();
            public JObject qualityGates = new JObject();
            public JObject designContract = new JObject();
            public JObject intentContract = new JObject();
            public JObject stateSemantics = new JObject();
            public JObject profileAvailability = new JObject();
            public JArray bindings = new JArray();
            public JObject artifactStatus = new JObject();
            public string generationMode = string.Empty;
            // Keep omission distinct from an authored layout object. A
            // default object would deserialize missing fields as zeroes and
            // silently collapse the screen root.
            public UiLayoutIntent rootLayoutIntent = null;
            public UiElement[] elements = Array.Empty<UiElement>();
        }

        [Serializable] private sealed class AuthoringResult
        {
            public string panelId;
            public string prefabPath;
            public string fixtureScenePath;
            public string status;
            public int elementCount;
            public string[] profiles;
            public string[] states;
            public string[] outputs;
        }

        // Fixture states are captured sequentially from one prefab instance. Keep
        // the authored presentation so each state starts from the same baseline.
        private sealed class FixtureStateBaseline
        {
            public readonly Dictionary<GameObject, bool> activeSelf = new Dictionary<GameObject, bool>();
            public readonly Dictionary<Graphic, Color> graphicColors = new Dictionary<Graphic, Color>();
            public readonly Dictionary<TMP_Text, bool> wrapping = new Dictionary<TMP_Text, bool>();
            public readonly Dictionary<TMP_Text, string> text = new Dictionary<TMP_Text, string>();
            public readonly Dictionary<Button, bool> interactable = new Dictionary<Button, bool>();
            public readonly Dictionary<Outline, bool> outlines = new Dictionary<Outline, bool>();
        }

        [Serializable] private sealed class UiTokens
        {
            public string background = "#121923";
            public string surface = "#1D2A39";
            public string surfaceRaised = "#26384A";
            public string accent = "#53B8FF";
            public string onAccent = "#07111D";
            public string text = "#F4F7FB";
            public string mutedText = "#98A8BA";
            public string danger = "#E86C73";
            public string onDanger = "#07111D";
            public int titleSize = 32;
            public int bodySize = 20;
            public int buttonSize = 22;
            public int labelSize = 20;
            public int captionSize = 16;
            public int numericSize = 20;
            public float spacing = 16f;
            public float padding = 28f;
        }

        [Serializable] private sealed class UiElement
        {
            public string id = "element";
            public string kind = "text";
            public string componentType = "";
            public string visualVariant = "default";
            public string[] assetSlots = Array.Empty<string>();
            public float value = 0f;
            public bool hasValue = false;
            public string text = "Text";
            public string colorToken = "text";
            public string typographyRole = "body";
            public string layerRole = "information";
            public int siblingOrder = -1;
            public string layout = "wide";
            public float width = 0f;
            public float height = 0f;
            public float minWidth = 0f;
            public float minHeight = 0f;
            public float maxWidth = 0f;
            public bool fillWidth = true;
            public bool interactable = false;
            public JObject interaction = new JObject();
            public JObject stateVariants = new JObject();
            public UiLayoutIntent layoutIntent = new UiLayoutIntent();
            public UiLayoutIntent narrowLayoutIntent = null;
            public UiContainerLayout layoutSpec = null;
            public UiElement[] children = Array.Empty<UiElement>();
            public bool wrapText = true;
            public int maxLines = 0;
            public string overflow = "wrap";
        }

        [Serializable] private sealed class UiContainerLayout
        {
            public string layoutMode = "absolute";
            public string axis = "vertical";
            public float gap = 0f;
            public float paddingLeft = 0f;
            public float paddingRight = 0f;
            public float paddingTop = 0f;
            public float paddingBottom = 0f;
            public int columns = 1;
            public float cellWidth = 0f;
            public float cellHeight = 0f;
            public float spacingX = 0f;
            public float spacingY = 0f;
            public string childAlignment = "upper-left";
            public bool controlChildWidth = true;
            public bool controlChildHeight = true;
            public bool forceChildExpandWidth = false;
            public bool forceChildExpandHeight = false;
            public string childGeometryOwner = "parent-layout-group";
        }

        [Serializable] private sealed class UiLayoutIntent
        {
            public string mode = "content";
            public float anchorMinX = 0f;
            public float anchorMinY = 0f;
            public float anchorMaxX = 1f;
            public float anchorMaxY = 1f;
            public float pivotX = 0.5f;
            public float pivotY = 0.5f;
            public float anchoredX = 0f;
            public float anchoredY = 0f;
            public float sizeWidth = 0f;
            public float sizeHeight = 0f;
            public bool ignoreParentLayout = false;
            public string anchorStrategy = "content";
            public string anchorEdge = "none";
            public string safeArea = "inherit";
        }


        /// <summary>
        /// 受 Automation UI Worker 调用的确定性 Editor 物化入口。
        /// 视觉 spec 由 AI 提供；此方法只负责校验、调用 Unity API 和返回产物身份。
        /// </summary>
        internal static string ExecuteAuthoringJsonCore(string specJson, string[] profileIds, string[] stateIds, bool dryRun,
            string contractSha256 = "", string runId = "", int sceneGeneration = 0, string evidenceRoot = "", string specHash = "")
        {
            if (string.IsNullOrWhiteSpace(specJson)) throw new InvalidDataException("UI spec 不能为空。");
            bool genericScreenSpec = ESUIScreenSpecAdapter.IsScreenSpecV3(specJson);
            if (!genericScreenSpec) throw new InvalidDataException("Only ScreenSpec v3 is accepted by the game UI materializer.");
            specJson = ESUIScreenSpecAdapter.Normalize(specJson);
            RejectUnknownUiSpecFields(JObject.Parse(specJson), "$spec");
            // JsonUtility has a hard nested-object depth limit that truncates real
            // component trees. Newtonsoft is already the project contract parser and
            // keeps ScreenSpec trees lossless before deterministic materialization.
            UiSpec spec = JsonConvert.DeserializeObject<UiSpec>(specJson);
            if (spec == null || string.IsNullOrWhiteSpace(spec.panelId)) throw new InvalidDataException("UI spec.panelId 缺失。");
            if (spec.elements == null) spec.elements = Array.Empty<UiElement>();
            Debug.Log($"[ESUIGameScreenMaterializer] Build={MaterializerBuildId}; panel={spec.panelId}; assets={spec.assets?.Length ?? 0}; elements={spec.elements.Length}");
            string[] profiles = NormalizeIds(profileIds, new[] { "landscape", "portrait" });
            string[] states = NormalizeIds(stateIds, new[] { "default" });
            ValidateSpec(spec, profiles);
            if (dryRun)
            {
                return JsonUtility.ToJson(new AuthoringResult
                {
                    panelId = spec.panelId,
                    prefabPath = spec.prefabPath,
                    fixtureScenePath = spec.fixtureScenePath,
                    status = "DryRun",
                    elementCount = spec.elements.Length,
                    profiles = profiles,
                    states = states,
                    outputs = Array.Empty<string>(),
                }, true);
            }
            return GenerateSpec(spec, profiles, states, contractSha256, runId, sceneGeneration, evidenceRoot, specHash);
        }

        /// <summary>
        /// Fixed Unity batch entrypoint for deterministic fixture regeneration. It accepts only
        /// project-relative UI contract inputs; it is not a general command/script runner.
        /// </summary>
        public static void RegenerateFromSpecBatchMode()
        {
            string specPath = ReadBatchArgument("-esUiSpecPath", string.Empty);
            string contractHash = ReadBatchArgument("-esUiContractHash", string.Empty);
            string specHash = ReadBatchArgument("-esUiSpecHash", string.Empty);
            string evidenceRoot = ReadBatchArgument("-esUiEvidenceRoot", string.Empty);
            string resultPath = ReadBatchArgument("-esUiResultPath", evidenceRoot.TrimEnd('/') + "/batch-materialization-result.json");
            string runId = ReadBatchArgument("-esUiRunId", Guid.NewGuid().ToString("N"));
            string[] profiles = ReadBatchIds("-esUiProfiles", new[] { "wide", "narrow" });
            string[] states = ReadBatchIds("-esUiStates", new[] { "default", "selected" });
            if (!specPath.StartsWith("Assets/UI/", StringComparison.Ordinal) || specPath.Contains(".."))
                throw new InvalidDataException("Batch UI spec 必须位于 Assets/UI/ 内。");
            if (!evidenceRoot.StartsWith("ES/UIEvidence/", StringComparison.Ordinal) || evidenceRoot.Contains(".."))
                throw new InvalidDataException("Batch UI evidenceRoot 必须位于 ES/UIEvidence/ 内。");
            if (!resultPath.StartsWith(evidenceRoot.TrimEnd('/') + "/", StringComparison.Ordinal) || resultPath.Contains(".."))
                throw new InvalidDataException("Batch UI resultPath 必须位于 evidenceRoot 内。");
            if (!ESAutomationWorkerRegistration.IsSha256(contractHash) || !ESAutomationWorkerRegistration.IsSha256(specHash))
                throw new InvalidDataException("Batch UI 必须提供 contractHash 和 specHash。");
            string specJson = File.ReadAllText(ESAutomationPathPolicy.Normalize(specPath), new UTF8Encoding(false, true));
            string result = ExecuteAuthoringJsonCore(specJson, profiles, states, false, contractHash, runId, 1, evidenceRoot, specHash);
            ESManagedFileIO.WriteTextAtomic(ESAutomationPathPolicy.Normalize(resultPath), result, new UTF8Encoding(false), ESAutomationPathPolicy.Normalize(evidenceRoot));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ESUIGameScreenMaterializer] Batch materialization completed: " + resultPath);
        }

        private static string ReadBatchArgument(string name, string fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int index = 0; index < args.Length - 1; index++)
                if (string.Equals(args[index], name, StringComparison.Ordinal)) return args[index + 1];
            return fallback;
        }

        private static string[] ReadBatchIds(string name, string[] fallback)
        {
            string value = ReadBatchArgument(name, string.Empty);
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            string[] ids = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim()).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.Ordinal).ToArray();
            return ids.Length == 0 ? fallback : ids;
        }

        private static string[] NormalizeIds(string[] values, string[] fallback)
        {
            if (values == null || values.Length == 0) return fallback;
            var result = new List<string>();
            foreach (string value in values)
            {
                string normalized = value?.Trim();
                if (string.IsNullOrWhiteSpace(normalized) || result.Exists(item => string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException("profile/state ID 必须非空且唯一。");
                result.Add(normalized);
            }
            return result.ToArray();
        }

        private static void ValidateSpec(UiSpec spec, string[] profiles = null)
        {
            ValidateAssetPath(spec.prefabPath, ".prefab");
            ValidateAssetPath(spec.fixtureScenePath, ".unity");
            if (spec.tokens == null) throw new InvalidDataException("UI spec.tokens 缺失。");
            ValidateAssetContract(spec);
            ValidateStateGeometryContract(spec);
            ValidateFixtureTextBindings(spec);
            ValidateAdvancedComposition(spec);
            // ScreenSpec v3 may omit a root layout. In that case the
            // materializer owns the canonical full-screen root geometry.
            // Reject an explicitly collapsed root before it can zero all UI.
            if (spec.rootLayoutIntent != null)
            {
                ValidateLayoutIntent(spec.rootLayoutIntent, "rootLayoutIntent");
                bool collapsed = Mathf.Approximately(spec.rootLayoutIntent.anchorMinX, spec.rootLayoutIntent.anchorMaxX)
                    && Mathf.Approximately(spec.rootLayoutIntent.anchorMinY, spec.rootLayoutIntent.anchorMaxY)
                    && spec.rootLayoutIntent.sizeWidth <= 0f
                    && spec.rootLayoutIntent.sizeHeight <= 0f;
                if (collapsed)
                    throw new InvalidDataException("Screen root layout cannot collapse to zero size.");
            }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (UiElement element in EnumerateElements(spec.elements))
            {
                if (element == null || string.IsNullOrWhiteSpace(element.id) || !ids.Add(element.id))
                    throw new InvalidDataException("UI spec 元素 ID 必须非空且唯一。");
                if (element.width < 0f || element.height < 0f || element.minWidth < 0f || element.maxWidth < 0f)
                    throw new InvalidDataException("UI spec 元素尺寸不得为负数。");
                ValidateLayoutIntent(element.layoutIntent, element.id);
                if (element.narrowLayoutIntent != null) ValidateLayoutIntent(element.narrowLayoutIntent, element.id + ".narrow");
                ValidateContainerLayout(element.layoutSpec, element.id);
                if (element.maxLines < 0) throw new InvalidDataException("UI spec maxLines 不得为负数：" + element.id);
                if (element.overflow != "wrap" && element.overflow != "truncate" && element.overflow != "ellipsis")
                    throw new InvalidDataException("UI spec overflow 必须为 wrap/truncate/ellipsis：" + element.id);
            }
        }

        private static IEnumerable<UiElement> EnumerateElements(IEnumerable<UiElement> roots)
        {
            foreach (UiElement element in roots ?? Array.Empty<UiElement>())
            {
                if (element == null) continue;
                yield return element;
                foreach (UiElement child in EnumerateElements(element.children)) yield return child;
            }
        }

        private static void ValidateContainerLayout(UiContainerLayout layout, string elementId)
        {
            if (layout == null) return;
            if (!new HashSet<string>(new[] { "absolute", "vertical", "horizontal", "grid", "overlay" }, StringComparer.OrdinalIgnoreCase).Contains(layout.layoutMode ?? string.Empty))
                throw new InvalidDataException("UI spec layoutSpec.layoutMode 无效：" + elementId);
            if (!new HashSet<string>(new[] { "vertical", "horizontal" }, StringComparer.OrdinalIgnoreCase).Contains(layout.axis ?? string.Empty))
                throw new InvalidDataException("UI spec layoutSpec.axis 无效：" + elementId);
            if (layout.gap < 0f || layout.paddingLeft < 0f || layout.paddingRight < 0f || layout.paddingTop < 0f || layout.paddingBottom < 0f
                || layout.cellWidth < 0f || layout.cellHeight < 0f || layout.spacingX < 0f || layout.spacingY < 0f || layout.columns < 1)
                throw new InvalidDataException("UI spec layoutSpec 尺寸、间距和列数不得为负数：" + elementId);
        }

        private static void ValidateLayoutIntent(UiLayoutIntent intent, string elementId)
        {
            if (intent == null) throw new InvalidDataException("UI spec layoutIntent 缺失：" + elementId);
            var modes = new HashSet<string>(new[] { "content", "fixed", "stretch", "centered", "edge-docked", "absolute" }, StringComparer.Ordinal);
            if (!modes.Contains(intent.mode)) throw new InvalidDataException("UI spec layoutIntent.mode 无效：" + elementId);
            foreach (float value in new[] { intent.anchorMinX, intent.anchorMinY, intent.anchorMaxX, intent.anchorMaxY, intent.pivotX, intent.pivotY, intent.anchoredX, intent.anchoredY, intent.sizeWidth, intent.sizeHeight })
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException("UI spec layoutIntent 必须为有限数：" + elementId);
            if (intent.anchorMinX < 0f || intent.anchorMinX > 1f || intent.anchorMinY < 0f || intent.anchorMinY > 1f
                || intent.anchorMaxX < 0f || intent.anchorMaxX > 1f || intent.anchorMaxY < 0f || intent.anchorMaxY > 1f
                || intent.anchorMinX > intent.anchorMaxX || intent.anchorMinY > intent.anchorMaxY)
                throw new InvalidDataException("UI spec Anchor 范围无效：" + elementId);
            if (intent.pivotX < 0f || intent.pivotX > 1f || intent.pivotY < 0f || intent.pivotY > 1f)
                throw new InvalidDataException("UI spec Pivot 范围无效：" + elementId);
            if (intent.sizeWidth < 0f || intent.sizeHeight < 0f)
                throw new InvalidDataException("UI spec layoutIntent 尺寸不得为负数：" + elementId);
        }

        private static void RejectUnknownUiSpecFields(JToken token, string path)
        {
            if (token is JObject obj)
            {
                bool extensible = path.EndsWith(".fixtureData", StringComparison.Ordinal)
                    || path.Contains(".stateVariants", StringComparison.Ordinal);
                foreach (JProperty property in obj.Properties())
                {
                    if (!extensible && !UiSpecFieldNames.Contains(property.Name))
                        throw new InvalidDataException("UI spec 包含未声明字段：" + path + "." + property.Name);
                    RejectUnknownUiSpecFields(property.Value, path + "." + property.Name);
                }
            }
            else if (token is JArray array)
            {
                for (int index = 0; index < array.Count; index++)
                    RejectUnknownUiSpecFields(array[index], path + "[" + index + "]");
            }
        }

        private static string GenerateSpec(UiSpec spec, string[] profiles, string[] states,
            string contractSha256 = "", string runId = "", int sceneGeneration = 0, string evidenceRoot = "", string specHash = "")
        {
            ValidateSpec(spec, profiles);
            EnsureParentFolder(spec.prefabPath);
            EnsureParentFolder(spec.fixtureScenePath);
            GameObject root = null;
            Scene fixture = default;
            Scene previous = SceneManager.GetActiveScene();
            var outputs = new List<string>();
            try
            {
                root = BuildRoot(spec);
                ReapplySpecGeometry(root, spec);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, spec.prefabPath);
                if (prefab == null) throw new InvalidOperationException("Prefab 保存失败");
                // BatchMode starts without a saved active Scene, so Single is the only valid
                // Editor API there. Interactive authoring keeps the user's scene additive.
                fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                    Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
                if (!fixture.IsValid()) throw new InvalidOperationException("Fixture Scene 创建失败。");
                SceneManager.SetActiveScene(fixture);
                GameObject canvas = new GameObject("UI_Fixture_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                RectTransform canvasRect = canvas.GetComponent<RectTransform>();
                Canvas c = canvas.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceCamera;
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                ApplyResponsivePolicy(c, scaler, spec);
                // Fixture capture is camera-backed even when the authored Prefab
                // policy is ScreenSpaceOverlay, so the RenderTexture receives UI pixels.
                c.renderMode = RenderMode.ScreenSpaceCamera;
                // Unity may reset a root RectTransform when Canvas.renderMode is
                // assigned. Apply geometry only after all Canvas properties exist.
                EnsureFixtureCanvasGeometry(canvasRect, 1920, 1080);
                GameObject cameraObject = new GameObject("UI_Fixture_Camera", typeof(Camera));
                Camera fixtureCamera = cameraObject.GetComponent<Camera>(); fixtureCamera.clearFlags = CameraClearFlags.Color; fixtureCamera.backgroundColor = ParseColor(spec.tokens.background, Color.black); fixtureCamera.orthographic = true; fixtureCamera.orthographicSize = 540f; fixtureCamera.transform.position = new Vector3(960f, 540f, -1000f); fixtureCamera.transform.rotation = Quaternion.identity;
                c.worldCamera = fixtureCamera; c.planeDistance = 1000f;
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
                ConfigureFixtureInstanceCanvas(instance, fixtureCamera, 1920, 1080);
                EnsureEventSystemInScene();
                // Canvas creation can leave a RectTransform at zero scale until its
                // components finish initialization. Reassert the invariant at the
                // serialization boundary so the Fixture Scene cannot collapse.
                EnsureFixtureCanvasGeometry(canvasRect, 1920, 1080);
                EditorUtility.SetDirty(canvas);
                if (!EditorSceneManager.SaveScene(fixture, spec.fixtureScenePath))
                    throw new InvalidOperationException("UI Fixture Scene 初次保存失败：" + spec.fixtureScenePath);
                FixtureStateBaseline baseline = CaptureFixtureStateBaseline(instance);
                foreach (string state in states)
                foreach (string profile in profiles)
                {
                    bool portrait = profile.IndexOf("portrait", StringComparison.OrdinalIgnoreCase) >= 0
                        || profile.IndexOf("narrow", StringComparison.OrdinalIgnoreCase) >= 0;
                    int width = portrait ? 1080 : 1920;
                    int height = portrait ? 1920 : 1080;
                    ResetFixtureState(instance, baseline);
                    ApplyFixtureProfile(instance, profile);
                    // Profile activation can invoke Unity Selectable lifecycle callbacks;
                    // apply the fixture state after activation so the captured Graphic is final.
                    ApplyFixtureState(instance, state, spec, baseline);
                    string output = CaptureFixture(spec, spec.panelId, fixtureCamera, canvasRect, instance.transform,
                        width, height, profile, state, contractSha256, specHash, runId, sceneGeneration, evidenceRoot, baseline);
                    outputs.Add(output);
                }
                if (instance != null) EditorUtility.SetDirty(instance);
                // Capture/profile callbacks may touch the active Canvas transform;
                // enforce the serialized invariant again before the final scene save.
                EnsureFixtureCanvasGeometry(canvasRect, 1920, 1080);
                EditorSceneManager.MarkSceneDirty(fixture);
                if (!EditorSceneManager.SaveScene(fixture, spec.fixtureScenePath))
                    throw new InvalidOperationException("UI Fixture Scene 最终保存失败：" + spec.fixtureScenePath);
                AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
                return JsonUtility.ToJson(new AuthoringResult
                {
                    panelId = spec.panelId, prefabPath = spec.prefabPath, fixtureScenePath = spec.fixtureScenePath,
                    status = "Completed", elementCount = spec.elements.Length, profiles = profiles, states = states,
                    outputs = outputs.ToArray(),
                }, true);
            }
            finally
            {
                try
                {
                    if (fixture.IsValid() && fixture.isLoaded)
                        EditorSceneManager.CloseScene(fixture, true);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
                finally
                {
                    try
                    {
                        if (previous.IsValid() && previous.isLoaded)
                            SceneManager.SetActiveScene(previous);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }

                    if (root != null)
                    {
                        try { UnityEngine.Object.DestroyImmediate(root); }
                        catch (Exception exception) { Debug.LogException(exception); }
                    }
                }
            }
        }

        private static GameObject BuildRoot(UiSpec spec)
        {
            // The authored artifact is a self-contained UGUI prefab. Runtime scenes only
            // instantiate this Canvas and bind existing controls; they never build UI nodes.
            ConfigureFontForSpec(spec);
            EnsureShowcaseFontCharacters(spec);
            GameObject root = new GameObject(spec.panelId, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasRenderer), typeof(Image), typeof(ESUIAdaptiveLayout));
            Canvas rootCanvas = root.GetComponent<Canvas>();
            AddAuthoredRuntimeBinder(root);
            rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            rootCanvas.sortingOrder = 500;
            CanvasScaler rootScaler = root.GetComponent<CanvasScaler>();
            ApplyResponsivePolicy(rootCanvas, rootScaler, spec);
            RectTransform rr = root.GetComponent<RectTransform>(); rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one; rr.offsetMin = Vector2.zero; rr.offsetMax = Vector2.zero;
            rr.localScale = Vector3.one;
            if (spec.rootLayoutIntent != null)
            {
                rr.anchorMin = new Vector2(spec.rootLayoutIntent.anchorMinX, spec.rootLayoutIntent.anchorMinY);
                rr.anchorMax = new Vector2(spec.rootLayoutIntent.anchorMaxX, spec.rootLayoutIntent.anchorMaxY);
                rr.pivot = new Vector2(spec.rootLayoutIntent.pivotX, spec.rootLayoutIntent.pivotY);
                rr.anchoredPosition = new Vector2(spec.rootLayoutIntent.anchoredX, spec.rootLayoutIntent.anchoredY);
                if (spec.rootLayoutIntent.sizeWidth > 0f || spec.rootLayoutIntent.sizeHeight > 0f)
                    rr.sizeDelta = new Vector2(spec.rootLayoutIntent.sizeWidth, spec.rootLayoutIntent.sizeHeight);
            }
            Image rootImage = root.GetComponent<Image>(); rootImage.sprite = ResolveAssetSprite(new[] { "lobby-background", "lobby-frame" }, spec);
            rootImage.color = HasDeclaredAsset(spec, "lobby-background") || HasDeclaredAsset(spec, "lobby-frame")
                ? Color.white
                : ParseColor(spec.tokens.background, Color.black);
            rootImage.raycastTarget = false;
            GameObject wide = BuildProfile(spec, "Wide", false); wide.transform.SetParent(root.transform, false);
            GameObject narrow = BuildProfile(spec, "Narrow", true); narrow.transform.SetParent(root.transform, false);
            ESUIAdaptiveLayout adaptive = root.GetComponent<ESUIAdaptiveLayout>(); adaptive.Configure(wide.transform as RectTransform, narrow.transform as RectTransform, spec.narrowAspectThreshold);
            return root;
        }

        private static readonly HashSet<string> StateGeometryMutationFields = new HashSet<string>(new[]
        {
            "bounds", "anchor", "pivot", "layout", "layoutmode", "mode", "minsize",
            "targetsize", "siblingorder", "childgeometryowner", "safearea", "canvas",
            "parent", "position", "size", "width", "height"
        }, StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> StateGeometryMutationLabels = new HashSet<string>(new[]
        {
            "bounds", "anchor", "pivot", "layout", "layout-mode", "min-size",
            "target-size", "sibling-order", "child-geometry-owner", "safe-area",
            "canvas", "parent", "position", "size"
        }, StringComparer.OrdinalIgnoreCase);

        private static void ValidateStateGeometryContract(UiSpec spec)
        {
            foreach (JProperty state in spec?.stateSemantics?.Properties() ?? Enumerable.Empty<JProperty>())
            {
                JObject semantics = state.Value as JObject;
                JObject policy = semantics?["geometryPolicy"] as JObject;
                if (policy?.Value<bool?>("preserveBounds") != true)
                    throw new InvalidDataException("ScreenSpec state geometry must preserve authored bounds: " + state.Name);
                JArray allowedChanges = policy["allowedChanges"] as JArray;
                foreach (JToken allowed in allowedChanges ?? new JArray())
                {
                    string label = allowed.Value<string>();
                    if (!string.IsNullOrWhiteSpace(label) && StateGeometryMutationLabels.Contains(label.Trim()))
                        throw new InvalidDataException("ScreenSpec state geometry cannot allow a layout mutation: " + state.Name + "." + label);
                }
                foreach (JObject effect in (semantics?["effects"] as JArray ?? new JArray()).OfType<JObject>())
                    RejectStateGeometryMutation(effect["changes"], "stateSemantics." + state.Name + ".effects");
            }
            foreach (UiElement element in EnumerateElements(spec?.elements))
                RejectStateGeometryMutation(element?.stateVariants, "elements." + (element?.id ?? string.Empty) + ".stateVariants");
        }

        private static void RejectStateGeometryMutation(JToken token, string path)
        {
            if (token is JObject obj)
            {
                foreach (JProperty property in obj.Properties())
                {
                    string normalized = (property.Name ?? string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);
                    if (StateGeometryMutationFields.Contains(normalized))
                        throw new InvalidDataException("ScreenSpec state-local geometry is forbidden; revise the base LayoutPlan: " + path + "." + property.Name);
                    RejectStateGeometryMutation(property.Value, path + "." + property.Name);
                }
            }
            else if (token is JArray array)
            {
                for (int index = 0; index < array.Count; index++)
                    RejectStateGeometryMutation(array[index], path + "[" + index + "]");
            }
        }

        private static void ValidateFixtureTextBindings(UiSpec spec)
        {
            Dictionary<string, UiElement> elements = EnumerateElements(spec?.elements)
                .Where(element => element != null && !string.IsNullOrWhiteSpace(element.id))
                .GroupBy(element => element.id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            foreach (JProperty state in spec?.stateSemantics?.Properties() ?? Enumerable.Empty<JProperty>())
            {
                JObject semantics = state.Value as JObject;
                if (semantics == null) continue;
                JArray affected = semantics["affectedComponentIds"] as JArray;
                var affectedIds = new HashSet<string>((affected ?? new JArray()).Values<string>().Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);
                JObject fixtureData = semantics["fixtureData"] as JObject;
                JArray bindings = semantics["fixtureTextBindings"] as JArray;
                var textualTargets = new HashSet<string>(affectedIds.Where(componentId => elements.TryGetValue(componentId, out UiElement element) && !string.IsNullOrWhiteSpace(element.text)), StringComparer.Ordinal);
                if (bindings == null)
                {
                    if (string.Equals(state.Name, "long-content", StringComparison.OrdinalIgnoreCase) && textualTargets.Count > 0)
                        throw new InvalidDataException("long-content must bind every affected textual component to fixtureData.");
                    continue;
                }
                var bindingIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (JObject binding in bindings.OfType<JObject>())
                {
                    string componentId = binding.Value<string>("componentId");
                    if (string.IsNullOrWhiteSpace(componentId) || !elements.TryGetValue(componentId, out UiElement element))
                        throw new InvalidDataException("fixtureTextBinding references an unknown component: " + state.Name);
                    if (!bindingIds.Add(componentId))
                        throw new InvalidDataException("fixtureTextBinding duplicates a component in state: " + state.Name + "." + componentId);
                    if (!affectedIds.Contains(componentId))
                        throw new InvalidDataException("fixtureTextBinding must target an affected component: " + state.Name + "." + componentId);
                    if (string.IsNullOrWhiteSpace(element.text) || element.interactable)
                        throw new InvalidDataException("fixtureTextBinding must target non-interactive authored text: " + state.Name + "." + componentId);
                    string key = binding.Value<string>("fixtureDataKey");
                    JToken textValue = string.IsNullOrWhiteSpace(key) || fixtureData == null ? null : fixtureData[key];
                    if (textValue?.Type != JTokenType.String || string.IsNullOrWhiteSpace(textValue.Value<string>()))
                        throw new InvalidDataException("fixtureTextBinding must resolve to a non-empty fixtureData string: " + state.Name + "." + componentId);
                    string overflowPolicy = binding.Value<string>("overflowPolicy");
                    if (overflowPolicy != "wrap" && overflowPolicy != "ellipsis")
                        throw new InvalidDataException("fixtureTextBinding overflowPolicy must be wrap or ellipsis until a scroll recipe exists: " + state.Name + "." + componentId);
                    if (binding.Value<int?>("maxLines") is int maxLines && maxLines > 0)
                    {
                        // Valid positive max line count.
                    }
                    else throw new InvalidDataException("fixtureTextBinding maxLines must be positive: " + state.Name + "." + componentId);
                    JArray insets = binding["contentInsetsPx"] as JArray;
                    if (insets == null || insets.Count != 4 || insets.Any(value => value.Type != JTokenType.Integer && value.Type != JTokenType.Float || value.Value<float>() < 0f))
                        throw new InvalidDataException("fixtureTextBinding needs four non-negative contentInsetsPx values: " + state.Name + "." + componentId);
                    float? clearance = binding.Value<float?>("reserveActionClearancePx");
                    if (!clearance.HasValue || clearance.Value < 0f)
                        throw new InvalidDataException("fixtureTextBinding reserveActionClearancePx must be non-negative: " + state.Name + "." + componentId);
                }
                if (string.Equals(state.Name, "long-content", StringComparison.OrdinalIgnoreCase) && !textualTargets.SetEquals(bindingIds))
                    throw new InvalidDataException("long-content fixtureTextBindings must cover exactly its affected textual components: " + state.Name);
                var effectTextTargets = new HashSet<string>(
                    (semantics["effects"] as JArray ?? new JArray()).OfType<JObject>()
                        .Where(effect => effect["changes"] is JObject changes && changes["text"] != null)
                        .Select(effect => effect.Value<string>("componentId"))
                        .Where(componentId => !string.IsNullOrWhiteSpace(componentId)),
                    StringComparer.Ordinal);
                if (bindingIds.Overlaps(effectTextTargets))
                    throw new InvalidDataException("fixtureTextBinding and state effect cannot both own text: " + state.Name);
            }
        }

        private static void ValidateAdvancedComposition(UiSpec spec)
        {
            JObject contract = spec?.designContract;
            JObject advanced = contract?["advancedComposition"] as JObject;
            if (advanced == null) return;
            Dictionary<string, UiElement> elements = EnumerateElements(spec.elements)
                .Where(element => element != null && !string.IsNullOrWhiteSpace(element.id))
                .GroupBy(element => element.id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var profiles = new HashSet<string>((spec.profiles ?? new JArray()).Values<string>("id")
                .Where(profile => !string.IsNullOrWhiteSpace(profile)), StringComparer.OrdinalIgnoreCase);
            if (profiles.Count == 0) throw new InvalidDataException("advancedComposition requires declared profiles.");
            string requestedIntent = spec.intentContract?.Value<string>("requestedPrimaryIntent");
            string primaryToken = (contract?["colorRoles"] as JObject)?.Value<string>("primaryAction");
            JArray primaryActions = advanced["primaryActions"] as JArray;
            if (primaryActions == null || primaryActions.Count != 1)
                throw new InvalidDataException("advancedComposition requires exactly one primary action.");
            var primaryLogicalIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (JObject action in primaryActions.OfType<JObject>())
            {
                string logicalId = action.Value<string>("logicalId");
                if (string.IsNullOrWhiteSpace(logicalId) || !primaryLogicalIds.Add(logicalId))
                    throw new InvalidDataException("advancedComposition primary action logicalId must be unique.");
                if (!string.Equals(action.Value<string>("intent"), requestedIntent, StringComparison.Ordinal))
                    throw new InvalidDataException("advancedComposition primary action intent must match the requested intent.");
                ValidateAdvancedProfileMapping(action["componentIdsByProfile"] as JObject, profiles, elements, requestedIntent, primaryToken, true, "primary action");
            }
            string focalTreatment = advanced.Value<string>("focalTreatment");
            JArray focalSubjects = advanced["focalSubjects"] as JArray;
            var focalSubjectByLogicalId = new Dictionary<string, JObject>(StringComparer.Ordinal);
            if (focalTreatment == "none")
            {
                if (string.IsNullOrWhiteSpace(advanced.Value<string>("noFocalReason")) || (focalSubjects != null && focalSubjects.Count != 0))
                    throw new InvalidDataException("advancedComposition focalTreatment none requires a reason and no focal subjects.");
            }
            else if (focalTreatment == "subject")
            {
                if (focalSubjects == null || focalSubjects.Count == 0)
                    throw new InvalidDataException("advancedComposition focalTreatment subject requires focal subjects.");
                foreach (JObject subject in focalSubjects.OfType<JObject>())
                {
                    string logicalId = subject.Value<string>("logicalId");
                    if (string.IsNullOrWhiteSpace(logicalId) || focalSubjectByLogicalId.ContainsKey(logicalId))
                        throw new InvalidDataException("advancedComposition focal subject logicalId must be unique.");
                    focalSubjectByLogicalId.Add(logicalId, subject);
                    ValidateAdvancedProfileMapping(subject["componentIdsByProfile"] as JObject, profiles, elements, null, null, false, "focal subject");
                }
            }
            else throw new InvalidDataException("advancedComposition focalTreatment must be subject or none.");
            ValidateAdvancedFocalAssetPolicies(advanced, focalSubjectByLogicalId, elements, spec.assets ?? Array.Empty<UiAsset>());
            ValidateAdvancedNamedCollection(advanced["alignmentGroups"] as JArray, "alignmentGroups");
            ValidateAdvancedNamedCollection(advanced["clearanceConstraints"] as JArray, "clearanceConstraints");
            JArray equivalences = advanced["responsiveEquivalences"] as JArray;
            if (equivalences == null || equivalences.Count == 0)
                throw new InvalidDataException("advancedComposition requires responsive equivalences.");
            var equivalenceLogicalIds = new HashSet<string>(equivalences.OfType<JObject>().Select(item => item.Value<string>("logicalId")).Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);
            if (!primaryLogicalIds.IsSubsetOf(equivalenceLogicalIds))
                throw new InvalidDataException("advancedComposition responsive equivalences must include the primary action.");
            foreach (JObject equivalence in equivalences.OfType<JObject>())
                ValidateAdvancedProfileMapping(equivalence["componentIdsByProfile"] as JObject, profiles, elements, equivalence.Value<string>("intent"), null, false, "responsive equivalence");
            ValidateAdvancedInteractionDensity(advanced, profiles, elements);
        }

        private static void ValidateAdvancedNamedCollection(JArray collection, string name)
        {
            if (collection == null || collection.Count == 0 || collection.Any(item => !(item is JObject)))
                throw new InvalidDataException("advancedComposition requires non-empty " + name + ".");
        }

        private static void ValidateAdvancedFocalAssetPolicies(JObject advanced, Dictionary<string, JObject> focalSubjects, Dictionary<string, UiElement> elements, UiAsset[] assets)
        {
            JArray policies = advanced["focalAssetPolicies"] as JArray;
            if (focalSubjects.Count == 0)
            {
                if (policies != null && policies.Count != 0)
                    throw new InvalidDataException("advancedComposition focalAssetPolicies must be empty without focal subjects.");
                return;
            }
            if (policies == null || policies.Count == 0)
                throw new InvalidDataException("advancedComposition focal subjects require focalAssetPolicies.");
            var assetsById = (assets ?? Array.Empty<UiAsset>()).Where(asset => asset != null && !string.IsNullOrWhiteSpace(asset.id))
                .GroupBy(asset => asset.id, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var policiesByLogicalId = new Dictionary<string, JObject>(StringComparer.Ordinal);
            foreach (JObject policy in policies.OfType<JObject>())
            {
                string logicalId = policy.Value<string>("logicalId");
                if (string.IsNullOrWhiteSpace(logicalId) || !focalSubjects.ContainsKey(logicalId) || policiesByLogicalId.ContainsKey(logicalId))
                    throw new InvalidDataException("advancedComposition focalAssetPolicy must name one focal subject exactly once.");
                policiesByLogicalId.Add(logicalId, policy);
                string cropPolicy = policy.Value<string>("cropPolicy");
                if (cropPolicy != "focal-cover" && cropPolicy != "contain" && cropPolicy != "no-crop")
                    throw new InvalidDataException("advancedComposition focalAssetPolicy cropPolicy is invalid.");
                JArray assetIds = policy["assetIds"] as JArray;
                if (assetIds == null || assetIds.Count == 0 || assetIds.Any(item => item.Type != JTokenType.String) || assetIds.Values<string>().Distinct(StringComparer.Ordinal).Count() != assetIds.Count)
                    throw new InvalidDataException("advancedComposition focalAssetPolicy assetIds must be a unique non-empty string list.");
                JArray focalPoint = policy["focalPoint"] as JArray;
                JArray insets = policy["safeCropInsetsNormalized"] as JArray;
                bool pointValid = focalPoint != null && focalPoint.Count == 2 && focalPoint.All(item => item.Type == JTokenType.Integer || item.Type == JTokenType.Float) && focalPoint.All(item => item.Value<float>() >= 0f && item.Value<float>() <= 1f);
                bool insetsValid = insets != null && insets.Count == 4 && insets.All(item => item.Type == JTokenType.Integer || item.Type == JTokenType.Float) && insets.All(item => item.Value<float>() >= 0f && item.Value<float>() < 1f);
                if (cropPolicy == "focal-cover" && (!pointValid || !insetsValid || insets[0].Value<float>() + insets[2].Value<float>() >= 1f || insets[1].Value<float>() + insets[3].Value<float>() >= 1f))
                    throw new InvalidDataException("advancedComposition focal-cover policy requires normalized focalPoint and safe crop insets.");
                foreach (string assetId in assetIds.Values<string>())
                {
                    if (!assetsById.TryGetValue(assetId, out UiAsset asset))
                        throw new InvalidDataException("advancedComposition focalAssetPolicy references an undeclared asset.");
                    if (!string.Equals(asset.cropPolicy, cropPolicy, StringComparison.Ordinal))
                        throw new InvalidDataException("advancedComposition focalAssetPolicy cropPolicy must match AssetManifest.");
                    if (cropPolicy == "focal-cover" && !JToken.DeepEquals(asset.focalPoint, focalPoint))
                        throw new InvalidDataException("advancedComposition focalAssetPolicy focalPoint must match AssetManifest.");
                    if (cropPolicy == "focal-cover" && (asset.sourceAspectRatio <= 0f || float.IsNaN(asset.sourceAspectRatio) || float.IsInfinity(asset.sourceAspectRatio)))
                        throw new InvalidDataException("advancedComposition focal-cover asset requires a positive finite sourceAspectRatio.");
                    if (cropPolicy == "focal-cover" && !string.Equals(asset.atlasRotationPolicy, "disallow-rotation", StringComparison.Ordinal))
                        throw new InvalidDataException("advancedComposition focal-cover asset requires atlasRotationPolicy disallow-rotation.");
                }
            }
            if (policiesByLogicalId.Count != focalSubjects.Count)
                throw new InvalidDataException("advancedComposition focalAssetPolicies must cover every focal subject.");
            foreach (KeyValuePair<string, JObject> subjectPair in focalSubjects)
            {
                JObject mapping = subjectPair.Value["componentIdsByProfile"] as JObject;
                var expectedAssets = new HashSet<string>(StringComparer.Ordinal);
                foreach (JProperty profile in mapping.Properties())
                    if (elements.TryGetValue(profile.Value.Value<string>(), out UiElement element))
                        foreach (string assetId in element.assetSlots ?? Array.Empty<string>()) expectedAssets.Add(assetId);
                var declaredAssets = new HashSet<string>((policiesByLogicalId[subjectPair.Key]["assetIds"] as JArray).Values<string>(), StringComparer.Ordinal);
                if (!expectedAssets.SetEquals(declaredAssets))
                    throw new InvalidDataException("advancedComposition focalAssetPolicy must cover exactly the focal subject asset slots.");
            }
        }

        private static void ValidateAdvancedInteractionDensity(JObject advanced, HashSet<string> profiles, Dictionary<string, UiElement> elements)
        {
            JObject density = advanced["interactionDensity"] as JObject;
            JArray groups = density?["groups"] as JArray;
            if (groups == null || groups.Count == 0 || groups.Any(item => !(item is JObject)))
                throw new InvalidDataException("advancedComposition requires non-empty interactionDensity groups.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (JObject group in groups.OfType<JObject>())
            {
                string id = group.Value<string>("id");
                string profileId = group.Value<string>("profileId");
                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id) || string.IsNullOrWhiteSpace(profileId) || !profiles.Contains(profileId))
                    throw new InvalidDataException("advancedComposition interactionDensity group id/profile is invalid.");
                JArray componentIds = group["componentIds"] as JArray;
                int? maxTargets = group.Value<int?>("maxTargets");
                float? minGap = group.Value<float?>("minGapPx");
                if (componentIds == null || componentIds.Count < 2 || componentIds.Any(item => item.Type != JTokenType.String) || componentIds.Values<string>().Distinct(StringComparer.Ordinal).Count() != componentIds.Count || !maxTargets.HasValue || maxTargets.Value < 1 || componentIds.Count > maxTargets.Value || !minGap.HasValue || minGap.Value < 0f || minGap.Value > 256f)
                    throw new InvalidDataException("advancedComposition interactionDensity group contract is invalid.");
                foreach (string componentId in componentIds.Values<string>())
                {
                    if (!elements.TryGetValue(componentId, out UiElement element))
                        throw new InvalidDataException("advancedComposition interactionDensity references an unknown component.");
                    if (!IsElementActiveInProfile(element, profileId))
                        throw new InvalidDataException("advancedComposition interactionDensity component is not active in its declared profile.");
                    JArray targetSize = element.interaction?["targetSize"] as JArray;
                    if (targetSize == null || targetSize.Count != 2 || targetSize.Any(item => item.Type != JTokenType.Integer && item.Type != JTokenType.Float) || targetSize.Any(item => item.Value<float>() <= 0f))
                        throw new InvalidDataException("advancedComposition interactionDensity target needs interaction.targetSize.");
                }
            }
        }

        private static bool IsElementActiveInProfile(UiElement element, string profileId)
        {
            string layout = element?.layout?.Trim().ToLowerInvariant() ?? string.Empty;
            return string.IsNullOrEmpty(layout) || layout == "both" || layout == "all" || string.Equals(layout, profileId, StringComparison.OrdinalIgnoreCase);
        }

        private static void ValidateAdvancedProfileMapping(JObject mapping, HashSet<string> profiles, Dictionary<string, UiElement> elements, string requiredIntent, string requiredColorToken, bool requireActionLayer, string subject)
        {
            if (mapping == null || mapping.Properties().Count() != profiles.Count || mapping.Properties().Any(property => !profiles.Contains(property.Name)))
                throw new InvalidDataException("advancedComposition " + subject + " must map every declared profile exactly once.");
            foreach (string profile in profiles)
            {
                string componentId = mapping.Value<string>(profile);
                if (string.IsNullOrWhiteSpace(componentId) || !elements.TryGetValue(componentId, out UiElement element))
                    throw new InvalidDataException("advancedComposition " + subject + " references an unknown component for profile " + profile + ".");
                if (!string.IsNullOrWhiteSpace(requiredIntent) && !string.Equals(element.interaction?.Value<string>("intent"), requiredIntent, StringComparison.Ordinal))
                    throw new InvalidDataException("advancedComposition " + subject + " must preserve its interaction intent.");
                if (!string.IsNullOrWhiteSpace(requiredColorToken) && !string.Equals(element.colorToken, requiredColorToken, StringComparison.Ordinal))
                    throw new InvalidDataException("advancedComposition primary action must preserve its color token.");
                if (requireActionLayer && !string.Equals(element.layerRole, "action", StringComparison.Ordinal))
                    throw new InvalidDataException("advancedComposition primary action must remain on the action layer.");
                if (subject == "focal subject" && (element.assetSlots == null || element.assetSlots.Length == 0))
                    throw new InvalidDataException("advancedComposition focal subject must bind an authored asset slot.");
                if (subject == "focal subject" && element.componentType != "image" && element.componentType != "icon" && element.componentType != "portrait")
                    throw new InvalidDataException("advancedComposition focal subject requires an image, icon or portrait renderer.");
                if (subject == "focal subject" && !string.Equals(element.visualVariant, "none", StringComparison.Ordinal))
                    throw new InvalidDataException("advancedComposition focal subject must not apply a token visual variant to authored art.");
            }
        }

        [Serializable] private sealed class UiAsset
        {
            public string id = string.Empty;
            public string role = string.Empty;
            public string source = "generated-placeholder";
            public string path = string.Empty;
            public string fallback = string.Empty;
            public string hash = string.Empty;
            public string provenance = string.Empty;
            public string license = string.Empty;
            public string importPolicy = string.Empty;
            public string aspectPolicy = string.Empty;
            public float sourceAspectRatio;
            public string atlasRotationPolicy = string.Empty;
            public JToken focalPoint;
            public string cropPolicy = string.Empty;
            public string nineSlice = string.Empty;
            public string atlasOwner = string.Empty;
            public JToken resolutionSet;
        }

        private static void AddAuthoredRuntimeBinder(GameObject root)
        {
            if (root == null)
                return;
            Type binderType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("ES.TestAssets.ESCompositeShaderUGUIBinder"))
                .FirstOrDefault(type => type != null);
            if (binderType != null && root.GetComponent(binderType) == null)
                root.AddComponent(binderType);
        }

        private static GameObject BuildProfile(UiSpec spec, string profileName, bool narrow)
        {
            GameObject profile = new GameObject(profileName, typeof(RectTransform));
            RectTransform pr = profile.GetComponent<RectTransform>(); pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one; pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;
            ApplyProfileSafeArea(pr, spec, narrow ? "narrow" : "wide");
            for (int i = 0; i < spec.elements.Length; i++)
            {
                UiElement element = spec.elements[i];
                if (!ShouldIncludeElement(element, narrow)) continue;
                CreateElement(element, profile.transform, spec, narrow, false);
            }
            profile.SetActive(!narrow);
            return profile;
        }

        private static bool ShouldIncludeElement(UiElement element, bool narrow)
        {
            if (element == null) return false;
            bool isNarrowElement = string.Equals(element.layout, "narrow", StringComparison.OrdinalIgnoreCase);
            return isNarrowElement == narrow || string.Equals(element.layout, "both", StringComparison.OrdinalIgnoreCase);
        }

        private static void CreateElement(UiElement data, Transform parent, UiSpec spec, bool narrow, bool parentManaged)
        {
            UiTokens tokens = spec.tokens;
            string kind = (data.kind ?? "text").ToLowerInvariant();
            GameObject go = new GameObject(string.IsNullOrWhiteSpace(data.id) ? kind : data.id, typeof(RectTransform)); go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            ESUIComponentSemantic semantic = go.AddComponent<ESUIComponentSemantic>();
            semantic.componentType = string.IsNullOrWhiteSpace(data.componentType) ? kind : data.componentType;
            semantic.visualVariant = string.IsNullOrWhiteSpace(data.visualVariant) ? "default" : data.visualVariant;
            semantic.colorToken = string.IsNullOrWhiteSpace(data.colorToken) ? "surface" : data.colorToken;
            semantic.typographyRole = string.IsNullOrWhiteSpace(data.typographyRole) ? "body" : data.typographyRole;
            semantic.layerRole = string.IsNullOrWhiteSpace(data.layerRole) ? "information" : data.layerRole;
            semantic.siblingOrder = data.siblingOrder;
            semantic.assetSlots = data.assetSlots ?? Array.Empty<string>();
            semantic.numericValue = data.value;
            semantic.hasNumericValue = data.hasValue;
            semantic.inputIntent = data.interaction == null ? string.Empty : data.interaction.Value<string>("intent") ?? string.Empty;
            JArray interactionTarget = data.interaction == null ? null : data.interaction["targetSize"] as JArray;
            semantic.interactionTargetWidth = interactionTarget != null && interactionTarget.Count > 0 ? interactionTarget[0].Value<int>() : 0;
            semantic.interactionTargetHeight = interactionTarget != null && interactionTarget.Count > 1 ? interactionTarget[1].Value<int>() : 0;
            if (data.siblingOrder >= 0) go.transform.SetSiblingIndex(data.siblingOrder);
            LayoutElement size = go.AddComponent<LayoutElement>();
            size.minWidth = data.minWidth; size.minHeight = Mathf.Max(data.height, data.minHeight); size.preferredWidth = data.width; size.preferredHeight = data.height > 0f ? data.height : data.minHeight; size.flexibleWidth = data.fillWidth ? 1f : 0f;
            ApplyLayoutIntent(rect, size, narrow && data.narrowLayoutIntent != null ? data.narrowLayoutIntent : data.layoutIntent, parentManaged);
            string semanticType = semantic.componentType.ToLowerInvariant();
            if (BuildSpecializedElement(go, data, semanticType, tokens))
            {
                // Specialized builders own their children and visual hierarchy.
            }
            else if (kind == "text" || kind == "title")
            {
                TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>(); ApplyShowcaseFont(text); text.text = data.text ?? string.Empty; text.fontSize = ResolveFontSize(data.typographyRole, kind, tokens); text.color = ParseVisualVariant(data.visualVariant, tokens, ParseToken(data.colorToken, tokens, Color.white)); text.alignment = TextAlignmentOptions.MidlineLeft; text.enableWordWrapping = data.wrapText; text.overflowMode = data.overflow == "wrap" ? TextOverflowModes.Overflow : data.overflow == "ellipsis" ? TextOverflowModes.Ellipsis : TextOverflowModes.Truncate; text.raycastTarget = false; if (data.maxLines > 0) text.maxVisibleLines = data.maxLines;
            }
            else if (kind == "button" || kind == "card" || kind == "panel")
            {
                Image image = go.AddComponent<Image>();
                // Every button asset is an icon slot. A square mode illustration
                // must never replace the authored button surface with a stretched
                // polygon or gradient.
                bool buttonOwnsFullAsset = kind != "button";
                image.sprite = buttonOwnsFullAsset ? ResolveAssetSprite(data.assetSlots, spec) : GetGeneratedUiSprite();
                image.color = kind == "button" && buttonOwnsFullAsset
                    ? Color.white
                    : kind == "button"
                    ? ParseVisualVariant(data.visualVariant, tokens, ParseToken(data.colorToken, tokens, ParseColor(tokens.surface, Color.gray)))
                    : ParseVisualVariant(data.visualVariant, tokens, ParseToken(data.colorToken, tokens, ParseColor(tokens.surface, Color.gray))); image.raycastTarget = kind == "button";
                if (kind == "button")
                {
                    Button button = go.AddComponent<Button>();
                    button.targetGraphic = image;
                    button.interactable = data.interactable;
                    // Fixture states own the captured color explicitly. Disable Unity's
                    // automatic ColorTint transition here so it cannot multiply the
                    // AI-authored accent a second time during an editor capture.
                    button.transition = Selectable.Transition.None;
                    Color accent = ParseVisualVariant(data.visualVariant, tokens, ParseToken(data.colorToken, tokens, ParseColor(tokens.accent, Color.cyan)));
                    Color authoredNormal = image.color;
                    ColorBlock colors = button.colors;
                    // Preserve the authored surface in the default fixture. The
                    // previous implementation assigned every button's normalColor
                    // to its accent token, turning an entire navigation bar into a
                    // single bright block and erasing semantic visual hierarchy.
                    colors.normalColor = authoredNormal;
                    colors.highlightedColor = Color.Lerp(authoredNormal, Color.white, 0.16f);
                    colors.pressedColor = Color.Lerp(authoredNormal, Color.black, 0.18f);
                    colors.selectedColor = Color.Lerp(authoredNormal, accent, 0.78f);
                    colors.disabledColor = new Color(authoredNormal.r, authoredNormal.g, authoredNormal.b, 0.35f);
                    button.colors = colors;
                    TextMeshProUGUI label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>(); label.transform.SetParent(go.transform, false); ApplyShowcaseFont(label); label.text = data.text ?? data.id; label.fontSize = ResolveFontSize(data.typographyRole, "button", tokens); label.color = ParseTextOnVariant(data.visualVariant, tokens, Color.white); label.alignment = TextAlignmentOptions.Center; label.enableWordWrapping = true; label.raycastTarget = false; Stretch(label.rectTransform);
                }
                else if (!string.IsNullOrWhiteSpace(data.text))
                {
                    TextMeshProUGUI label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>(); label.transform.SetParent(go.transform, false); ApplyShowcaseFont(label); label.text = data.text; label.fontSize = tokens.bodySize; label.color = ParseColor(tokens.mutedText, Color.gray); label.alignment = TextAlignmentOptions.TopLeft; label.enableWordWrapping = false; label.raycastTarget = false; label.rectTransform.anchorMin = new Vector2(0.04f, 0.88f); label.rectTransform.anchorMax = new Vector2(0.96f, 0.98f); label.rectTransform.offsetMin = Vector2.zero; label.rectTransform.offsetMax = Vector2.zero;
                }
                if (kind == "panel" && string.Equals(data.componentType, "frame", StringComparison.OrdinalIgnoreCase))
                    AddFrameTreatment(go, tokens);
            }
            else if (kind == "spacer") size.minHeight = Mathf.Max(size.minHeight, tokens.spacing);
            else if (kind == "image") { Image image = go.AddComponent<Image>(); image.sprite = ResolveAssetSprite(data.assetSlots, spec); image.color = ParseVisualVariant(data.visualVariant, tokens, ParseToken(data.colorToken, tokens, Color.white)); image.preserveAspect = true; }
            ApplyResolvedAssetVisuals(go, data, spec);
            // Specialized list/tab-bar builders already own their internal layout
            // container. Adding a second group here causes nested control conflicts.
            if (semanticType != "list" && semanticType != "tab-bar")
                ConfigureContainer(go, data.layoutSpec);
            bool childrenManaged = data.layoutSpec != null
                && string.Equals(data.layoutSpec.childGeometryOwner, "parent-layout-group", StringComparison.OrdinalIgnoreCase)
                && (string.Equals(data.layoutSpec.layoutMode, "vertical", StringComparison.OrdinalIgnoreCase)
                || string.Equals(data.layoutSpec.layoutMode, "horizontal", StringComparison.OrdinalIgnoreCase)
                || string.Equals(data.layoutSpec.layoutMode, "grid", StringComparison.OrdinalIgnoreCase));
            Transform childParent = go.transform;
            ScrollRect authoredScroll = go.GetComponent<ScrollRect>();
            if (authoredScroll != null && authoredScroll.content != null)
                childParent = authoredScroll.content;
            foreach (UiElement child in data.children ?? Array.Empty<UiElement>())
                if (ShouldIncludeElement(child, narrow)) CreateElement(child, childParent, spec, narrow, childrenManaged);
            // TMP and Button components initialize their RectTransform defaults. Re-apply the
            // AI-authored geometry after component creation so those defaults cannot leak into
            // the materialized layout.
            ApplyLayoutIntent(rect, size, narrow && data.narrowLayoutIntent != null ? data.narrowLayoutIntent : data.layoutIntent, parentManaged);
            // A validated layoutIntent is the sole geometry authority. Legacy width/height
            // values remain available to LayoutElement for older specs without an intent,
            // but must never overwrite AI-authored RectTransform geometry.
                if (data.layoutIntent == null && (data.width > 0f || data.height > 0f))
                rect.sizeDelta = new Vector2(data.width, data.height);
        }

        private static void ApplyResponsivePolicy(Canvas canvas, CanvasScaler scaler, UiSpec spec)
        {
            if (canvas == null || scaler == null) return;
            JObject policy = spec == null || spec.qualityGates == null ? null : spec.qualityGates["responsivePolicy"] as JObject;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            string renderMode = policy?.Value<string>("canvasRenderMode");
            if (string.Equals(renderMode, "ScreenSpaceCamera", StringComparison.OrdinalIgnoreCase))
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
            else if (string.Equals(renderMode, "WorldSpace", StringComparison.OrdinalIgnoreCase))
                canvas.renderMode = RenderMode.WorldSpace;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            JArray reference = policy == null ? null : policy["referenceResolution"] as JArray;
            if (reference != null && reference.Count == 2)
                scaler.referenceResolution = new Vector2(reference[0].Value<float>(), reference[1].Value<float>());
            else
                scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = policy?.Value<float?>("matchWidthOrHeight") ?? 0.5f;
        }

        private static void ApplyProfileSafeArea(RectTransform profile, UiSpec spec, string profileId)
        {
            if (profile == null || spec == null || spec.qualityGates == null) return;
            JObject policy = spec.qualityGates["responsivePolicy"] as JObject;
            if (!string.Equals(policy?.Value<string>("safeAreaPolicy"), "profile-safe-area-inset", StringComparison.OrdinalIgnoreCase)) return;
            JObject authored = (spec.profiles ?? new JArray()).OfType<JObject>()
                .FirstOrDefault(item => string.Equals(item.Value<string>("id"), profileId, StringComparison.OrdinalIgnoreCase));
            JArray safeArea = authored == null ? null : authored["safeArea"] as JArray;
            if (safeArea == null || safeArea.Count != 4) return;
            float minX = Mathf.Clamp01(safeArea[0].Value<float>());
            float minY = Mathf.Clamp01(safeArea[1].Value<float>());
            float maxX = Mathf.Clamp01(safeArea[2].Value<float>());
            float maxY = Mathf.Clamp01(safeArea[3].Value<float>());
            if (minX > maxX || minY > maxY) throw new InvalidDataException("profile safeArea bounds 无效：" + profileId);
            profile.anchorMin = new Vector2(minX, 1f - maxY);
            profile.anchorMax = new Vector2(maxX, 1f - minY);
            profile.offsetMin = Vector2.zero;
            profile.offsetMax = Vector2.zero;
        }

        private static void AddFrameTreatment(GameObject go, UiTokens tokens)
        {
            Image surface = go.GetComponent<Image>();
            if (surface == null) return;
            surface.raycastTarget = false;
            Color accent = ParseColor(tokens.accent, new Color(0.35f, 0.8f, 1f, 1f));
            Outline outline = go.GetComponent<Outline>() ?? go.AddComponent<Outline>();
            outline.effectColor = new Color(accent.r, accent.g, accent.b, 0.28f);
            outline.effectDistance = new Vector2(2f, 2f);
            outline.useGraphicAlpha = true;
            CreateVisualChild(go, "TopRule", new Color(accent.r, accent.g, accent.b, 0.55f), 0.015f, 0.965f, 0.985f, 0.985f);
        }

        private static bool BuildSpecializedElement(GameObject go, UiElement data, string semanticType, UiTokens tokens)
        {
            switch (semanticType)
            {
                case "item-slot": return BuildItemSlot(go, data, tokens, false);
                case "item-card": return BuildItemSlot(go, data, tokens, true);
                case "progress":
                case "bar": return BuildProgress(go, data, tokens);
                case "counter":
                case "badge":
                case "status-badge":
                case "input-hint": return BuildBadge(go, data, tokens);
                case "stat-row": return BuildStatRow(go, data, tokens);
                case "cooldown": return BuildCooldown(go, data, tokens);
                case "slider": return BuildSlider(go, data, tokens);
                case "toggle": return BuildToggle(go, data, tokens);
                case "list": return BuildScrollList(go, data, tokens);
                case "tab-bar": return BuildTabBar(go, data, tokens);
                case "dropdown": return BuildDropdown(go, data, tokens);
                case "tooltip": return BuildTooltip(go, data, tokens);
                case "loading": return BuildStatePanel(go, data, tokens, "Loading...");
                case "error-state": return BuildStatePanel(go, data, tokens, string.IsNullOrWhiteSpace(data.text) ? "Unable to load" : data.text);
                case "empty-state": return BuildStatePanel(go, data, tokens, string.IsNullOrWhiteSpace(data.text) ? "Nothing here yet" : data.text);
                case "focus-ring": return BuildFocusRing(go, tokens);
                default: return false;
            }
        }

        private static bool BuildItemSlot(GameObject go, UiElement data, UiTokens tokens, bool card)
        {
            Image surface = go.AddComponent<Image>(); surface.sprite = GetGeneratedUiSprite();
            surface.color = ParseToken(data.colorToken, tokens, ParseColor(tokens.surface, Color.gray));
            surface.raycastTarget = data.interactable;
            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = ParseColor(tokens.accent, Color.cyan);
            outline.effectDistance = new Vector2(2f, 2f);
            outline.enabled = string.Equals(data.visualVariant, "selected", StringComparison.OrdinalIgnoreCase);
            GameObject icon = CreateVisualChild(go, "Icon", ParseColor(tokens.accent, Color.cyan), 0.14f, 0.18f, 0.86f, card ? 0.82f : 0.86f);
            icon.GetComponent<Image>().preserveAspect = true;
            if (card)
            {
                CreateChildText(go, "Name", data.text ?? data.id, tokens.bodySize, ParseColor(tokens.text, Color.white), 0.08f, 0.02f, 0.92f, 0.18f, TextAlignmentOptions.Center);
                CreateChildText(go, "Rarity", string.IsNullOrWhiteSpace(data.visualVariant) ? "Common" : data.visualVariant, Mathf.Max(12, tokens.bodySize - 5), ParseColor(tokens.accent, Color.cyan), 0.08f, 0.82f, 0.92f, 0.98f, TextAlignmentOptions.Center);
            }
            CreateChildText(go, "Quantity", data.hasValue ? Mathf.RoundToInt(data.value).ToString() : "1", Mathf.Max(12, tokens.bodySize - 4), ParseColor(tokens.text, Color.white), 0.65f, 0.03f, 0.97f, 0.25f, TextAlignmentOptions.BottomRight);
            return true;
        }

        private static bool BuildProgress(GameObject go, UiElement data, UiTokens tokens)
        {
            Image background = go.AddComponent<Image>(); background.sprite = GetGeneratedUiSprite();
            background.color = new Color(0.04f, 0.06f, 0.09f, 0.92f);
            background.raycastTarget = false;
            GameObject fill = CreateVisualChild(go, "Fill", ParseColor(tokens.accent, Color.cyan), 0f, 0f, Mathf.Clamp01(data.hasValue ? data.value : 1f), 1f);
            fill.GetComponent<RectTransform>().pivot = new Vector2(0f, 0.5f);
            if (!string.IsNullOrWhiteSpace(data.text))
                CreateChildText(go, "Value", data.text, Mathf.Max(12, tokens.bodySize - 4), ParseColor(tokens.text, Color.white), 0f, 0f, 1f, 1f, TextAlignmentOptions.Center);
            return true;
        }

        private static bool BuildBadge(GameObject go, UiElement data, UiTokens tokens)
        {
            Image image = go.AddComponent<Image>(); image.sprite = GetGeneratedUiSprite();
            image.color = ParseToken(data.colorToken, tokens, ParseColor(tokens.accent, Color.cyan));
            image.raycastTarget = data.interactable;
            CreateChildText(go, "Label", data.hasValue ? Mathf.RoundToInt(data.value).ToString() : (data.text ?? data.id), Mathf.Max(12, tokens.bodySize - 3), ParseColor(tokens.text, Color.white), 0f, 0f, 1f, 1f, TextAlignmentOptions.Center);
            return true;
        }

        private static bool BuildCooldown(GameObject go, UiElement data, UiTokens tokens)
        {
            Image image = go.AddComponent<Image>(); image.sprite = GetGeneratedUiSprite(); image.color = ParseColor(tokens.surface, Color.gray); image.preserveAspect = true;
            GameObject overlay = CreateVisualChild(go, "CooldownFill", new Color(0f, 0f, 0f, 0.58f), 0f, 0f, 1f, Mathf.Clamp01(data.hasValue ? data.value : 0f));
            overlay.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0f);
            CreateChildText(go, "Seconds", data.hasValue ? Mathf.CeilToInt(data.value).ToString() : "", tokens.bodySize, ParseColor(tokens.text, Color.white), 0f, 0f, 1f, 1f, TextAlignmentOptions.Center);
            return true;
        }

        private static bool BuildStatRow(GameObject go, UiElement data, UiTokens tokens)
        {
            Image image = go.AddComponent<Image>();
            image.sprite = GetGeneratedUiSprite();
            image.color = ParseToken(data.colorToken, tokens, ParseColor(tokens.surface, Color.gray));
            image.raycastTarget = false;
            CreateVisualChild(go, "Avatar", ParseColor(tokens.accent, Color.cyan), 0.03f, 0.14f, 0.16f, 0.86f);
            CreateChildText(go, "Label", data.text ?? data.id, Mathf.Max(12, tokens.bodySize - 2), ParseColor(tokens.text, Color.white), 0.20f, 0.08f, 0.96f, 0.92f, TextAlignmentOptions.MidlineLeft);
            return true;
        }

        private static bool BuildSlider(GameObject go, UiElement data, UiTokens tokens)
        {
            Image background = go.AddComponent<Image>();
            background.sprite = GetGeneratedUiSprite();
            background.color = new Color(0.04f, 0.08f, 0.12f, 0.98f);
            background.raycastTarget = true;
            Slider slider = go.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = data.hasValue ? Mathf.Clamp01(data.value) : 0.5f;
            slider.direction = Slider.Direction.LeftToRight;
            GameObject fill = CreateVisualChild(go, "Fill", ParseColor(tokens.accent, Color.cyan), 0f, 0.25f, slider.value, 0.75f);
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.pivot = new Vector2(0f, 0.5f);
            slider.fillRect = fillRect;
            GameObject handle = CreateVisualChild(go, "Handle", Color.white, slider.value - 0.018f, 0.18f, slider.value + 0.018f, 0.82f);
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(slider.value, 0.5f);
            handleRect.anchorMax = new Vector2(slider.value, 0.5f);
            handleRect.sizeDelta = new Vector2(Mathf.Max(18f, tokens.bodySize), Mathf.Max(18f, tokens.bodySize));
            handleRect.anchoredPosition = Vector2.zero;
            slider.handleRect = handleRect;
            CreateChildText(go, "Label", data.text ?? data.id, Mathf.Max(12, tokens.bodySize - 3), ParseColor(tokens.text, Color.white), 0.03f, 0.05f, 0.52f, 0.95f, TextAlignmentOptions.Left);
            return true;
        }

        private static bool BuildScrollList(GameObject go, UiElement data, UiTokens tokens)
        {
            Image background = go.AddComponent<Image>();
            background.sprite = GetGeneratedUiSprite();
            background.color = ParseColor(tokens.surface, new Color(0.05f, 0.10f, 0.15f, 0.96f));
            background.raycastTarget = true;
            bool horizontal = data != null && data.layoutSpec != null
                && string.Equals(data.layoutSpec.axis, "horizontal", StringComparison.OrdinalIgnoreCase);
            ScrollRect scroll = go.AddComponent<ScrollRect>();
            scroll.horizontal = horizontal;
            scroll.vertical = !horizontal;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(go.transform, false);
            Stretch(viewport.GetComponent<RectTransform>());
            GameObject content = horizontal
                ? new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter))
                : new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = horizontal ? new Vector2(0f, 0f) : new Vector2(0f, 1f);
            contentRect.anchorMax = horizontal ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
            contentRect.pivot = horizontal ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;
            float gap = data.layoutSpec != null && data.layoutSpec.gap > 0f ? data.layoutSpec.gap : tokens.spacing;
            int padding = Mathf.Max(0, Mathf.RoundToInt(tokens.padding));
            if (horizontal)
            {
                HorizontalLayoutGroup layout = content.GetComponent<HorizontalLayoutGroup>();
                layout.spacing = gap;
                layout.padding = new RectOffset(padding, padding, padding, padding);
                layout.childControlWidth = false;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = true;
            }
            else
            {
                VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
                layout.spacing = gap;
                layout.padding = new RectOffset(padding, padding, padding, padding);
                layout.childControlWidth = true;
                layout.childControlHeight = false;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
            }
            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = horizontal ? ContentSizeFitter.FitMode.Unconstrained : ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = horizontal ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = contentRect;
            return true;
        }

        private static bool BuildTabBar(GameObject go, UiElement data, UiTokens tokens)
        {
            Image background = go.AddComponent<Image>();
            background.sprite = GetGeneratedUiSprite();
            background.color = ParseColor(tokens.surface, new Color(0.05f, 0.10f, 0.15f, 0.96f));
            background.raycastTarget = false;
            HorizontalLayoutGroup layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = tokens.spacing;
            int padding = Mathf.Max(0, Mathf.RoundToInt(tokens.padding * 0.5f));
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleCenter;
            return true;
        }

        private static bool BuildToggle(GameObject go, UiElement data, UiTokens tokens)
        {
            Image background = go.AddComponent<Image>();
            background.sprite = GetGeneratedUiSprite();
            background.color = ParseToken(data.colorToken, tokens, ParseColor(tokens.surface, Color.gray));
            background.raycastTarget = true;
            Toggle toggle = go.AddComponent<Toggle>();
            toggle.isOn = data.hasValue && data.value > 0.5f;
            GameObject checkmark = CreateVisualChild(go, "Checkmark", ParseColor(tokens.accent, Color.cyan), 0.03f, 0.22f, 0.07f, 0.78f);
            toggle.graphic = checkmark.GetComponent<Image>();
            CreateChildText(go, "Label", data.text ?? data.id, Mathf.Max(12, tokens.bodySize - 3), ParseColor(tokens.text, Color.white), 0.08f, 0.05f, 0.92f, 0.95f, TextAlignmentOptions.Left);
            return true;
        }

        private static bool BuildDropdown(GameObject go, UiElement data, UiTokens tokens)
        {
            Image background = go.AddComponent<Image>();
            background.sprite = GetGeneratedUiSprite();
            background.color = ParseToken(data.colorToken, tokens, ParseColor(tokens.surface, Color.gray));
            background.raycastTarget = true;
            Dropdown dropdown = go.AddComponent<Dropdown>();
            dropdown.captionText = CreateLegacyChildText(go, "Caption", data.text ?? data.id, tokens.buttonSize, ParseColor(tokens.text, Color.white), 0.08f, 0f, 0.9f, 1f);
            dropdown.options.Clear();
            dropdown.options.Add(new Dropdown.OptionData(data.text ?? data.id));
            return true;
        }

        private static bool BuildTooltip(GameObject go, UiElement data, UiTokens tokens)
        {
            Image image = go.AddComponent<Image>(); image.sprite = GetGeneratedUiSprite(); image.color = new Color(0.02f, 0.03f, 0.05f, 0.96f);
            CreateChildText(go, "Title", data.text ?? data.id, tokens.bodySize, ParseColor(tokens.text, Color.white), 0.08f, 0.56f, 0.92f, 0.92f, TextAlignmentOptions.TopLeft);
            CreateChildText(go, "Description", "Details", Mathf.Max(12, tokens.bodySize - 4), ParseColor(tokens.mutedText, Color.gray), 0.08f, 0.08f, 0.92f, 0.52f, TextAlignmentOptions.TopLeft);
            return true;
        }

        private static bool BuildStatePanel(GameObject go, UiElement data, UiTokens tokens, string fallback)
        {
            Image image = go.AddComponent<Image>(); image.sprite = GetGeneratedUiSprite(); image.color = ParseColor(tokens.surface, Color.gray);
            CreateChildText(go, "State", string.IsNullOrWhiteSpace(data.text) ? fallback : data.text, tokens.bodySize, ParseColor(tokens.mutedText, Color.gray), 0.08f, 0.15f, 0.92f, 0.85f, TextAlignmentOptions.Center);
            return true;
        }

        private static bool BuildFocusRing(GameObject go, UiTokens tokens)
        {
            Image image = go.AddComponent<Image>(); image.sprite = GetGeneratedUiSprite(); image.color = new Color(0f, 0f, 0f, 0f); image.type = Image.Type.Sliced;
            Outline outline = go.AddComponent<Outline>(); outline.effectColor = ParseColor(tokens.accent, Color.cyan); outline.effectDistance = new Vector2(3f, 3f);
            return true;
        }

        private static GameObject CreateVisualChild(GameObject parent, string name, Color color, float minX, float minY, float maxX, float maxY)
        {
            GameObject child = new GameObject(name, typeof(RectTransform), typeof(Image)); child.transform.SetParent(parent.transform, false);
            RectTransform rect = child.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(minX, minY); rect.anchorMax = new Vector2(maxX, maxY); rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            Image image = child.GetComponent<Image>(); image.sprite = GetGeneratedUiSprite(); image.color = color; image.raycastTarget = false;
            return child;
        }

        private static readonly Dictionary<string, Sprite> ProceduralAssetCache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        private static void ApplyResolvedAssetVisuals(GameObject go, UiElement data, UiSpec spec)
        {
            if (go == null || data == null || data.assetSlots == null || data.assetSlots.Length == 0) return;
            Sprite sprite = ResolveAssetSprite(data.assetSlots, spec);
            if (sprite == null) return;
            string type = (data.componentType ?? data.kind ?? string.Empty).ToLowerInvariant();
            if (TryResolveFocalCoverAsset(spec, data, out UiAsset focalAsset, out JObject focalPolicy) && (type == "image" || type == "icon" || type == "portrait"))
            {
                ConfigureFocalCoverGraphic(go, sprite, focalAsset, focalPolicy);
                return;
            }
            if (type == "stat-row")
            {
                Transform avatar = go.transform.Find("Avatar");
                Image avatarImage = avatar == null ? null : avatar.GetComponent<Image>();
                if (avatarImage != null) { avatarImage.sprite = sprite; avatarImage.preserveAspect = true; }
                return;
            }
            if (type == "status-badge" || type == "badge")
            {
                // Badges own a text-bearing surface. Put a declared emblem in a
                // stable child slot instead of stretching a square sprite across
                // the entire badge (the old behavior produced a distorted ribbon).
                Transform emblem = go.transform.Find("Asset");
                if (emblem == null)
                {
                    GameObject child = new GameObject("Asset", typeof(RectTransform), typeof(Image));
                    child.transform.SetParent(go.transform, false);
                    RectTransform childRect = child.GetComponent<RectTransform>();
                    childRect.anchorMin = new Vector2(0.04f, 0.16f);
                    childRect.anchorMax = new Vector2(0.22f, 0.84f);
                    childRect.offsetMin = Vector2.zero;
                    childRect.offsetMax = Vector2.zero;
                    emblem = child.transform;
                }
                Image emblemImage = emblem.GetComponent<Image>();
                if (emblemImage != null)
                {
                    emblemImage.sprite = sprite;
                    emblemImage.preserveAspect = true;
                    emblemImage.raycastTarget = false;
                }
                return;
            }
            if (type == "button")
            {
                Transform assetTransform = go.transform.Find("Asset");
                if (assetTransform == null)
                {
                    GameObject child = new GameObject("Asset", typeof(RectTransform), typeof(Image));
                    child.transform.SetParent(go.transform, false);
                    RectTransform childRect = child.GetComponent<RectTransform>();
                    childRect.anchorMin = new Vector2(0.08f, 0.18f);
                    childRect.anchorMax = new Vector2(0.28f, 0.82f);
                    childRect.offsetMin = Vector2.zero;
                    childRect.offsetMax = Vector2.zero;
                    assetTransform = child.transform;
                }
                Image assetImage = assetTransform.GetComponent<Image>();
                if (assetImage != null)
                {
                    assetImage.sprite = sprite;
                    assetImage.preserveAspect = true;
                    assetImage.raycastTarget = false;
                }
                return;
            }
            Image rootImage = go.GetComponent<Image>();
            if (rootImage != null)
            {
                rootImage.sprite = sprite;
                rootImage.preserveAspect = type == "image" || type == "icon" || type == "portrait";
            }
        }

        private static bool TryResolveFocalCoverAsset(UiSpec spec, UiElement data, out UiAsset asset, out JObject focalPolicy)
        {
            asset = null;
            focalPolicy = null;
            JObject advanced = spec?.designContract?["advancedComposition"] as JObject;
            JArray subjects = advanced?["focalSubjects"] as JArray;
            JArray policies = advanced?["focalAssetPolicies"] as JArray;
            if (subjects == null || policies == null || data == null) return false;
            foreach (JObject subject in subjects.OfType<JObject>())
            {
                JObject mapping = subject["componentIdsByProfile"] as JObject;
                if (mapping == null || !mapping.Properties().Any(property => string.Equals(property.Value.Value<string>(), data.id, StringComparison.Ordinal))) continue;
                string logicalId = subject.Value<string>("logicalId");
                JObject policy = policies.OfType<JObject>().FirstOrDefault(item => string.Equals(item.Value<string>("logicalId"), logicalId, StringComparison.Ordinal));
                if (policy == null || !string.Equals(policy.Value<string>("cropPolicy"), "focal-cover", StringComparison.Ordinal)) return false;
                foreach (string assetId in (policy["assetIds"] as JArray ?? new JArray()).Values<string>())
                {
                    if (!(data.assetSlots ?? Array.Empty<string>()).Contains(assetId, StringComparer.Ordinal)) continue;
                    asset = (spec.assets ?? Array.Empty<UiAsset>()).FirstOrDefault(candidate => candidate != null && string.Equals(candidate.id, assetId, StringComparison.Ordinal));
                    focalPolicy = asset == null ? null : policy;
                    return asset != null && focalPolicy != null;
                }
            }
            return false;
        }

        private static void ConfigureFocalCoverGraphic(GameObject go, Sprite sprite, UiAsset asset, JObject focalPolicy)
        {
            Image existingImage = go.GetComponent<Image>();
            if (existingImage != null)
            {
                existingImage.enabled = false;
                existingImage.raycastTarget = false;
            }
            ESUIFocalCropRawImage focalImage = go.GetComponent<ESUIFocalCropRawImage>();
            if (focalImage == null) focalImage = go.AddComponent<ESUIFocalCropRawImage>();
            if (sprite.packed && sprite.packingRotation != SpritePackingRotation.None)
                throw new InvalidDataException("focal-cover does not support a SpriteAtlas-rotated Sprite; set the atlas to disallow rotation.");
            JArray focal = asset?.focalPoint as JArray;
            JArray insets = focalPolicy?["safeCropInsetsNormalized"] as JArray;
            Vector2 focalPoint = focal != null && focal.Count == 2 ? new Vector2(focal[0].Value<float>(), focal[1].Value<float>()) : new Vector2(0.5f, 0.5f);
            Rect safeInsets = insets != null && insets.Count == 4
                ? new Rect(insets[0].Value<float>(), insets[1].Value<float>(), insets[2].Value<float>(), insets[3].Value<float>())
                : new Rect(0f, 0f, 0f, 0f);
            focalImage.Configure(sprite, focalPoint, safeInsets);
            float declaredAspect = asset?.sourceAspectRatio ?? 0f;
            float actualAspect = focalImage.SourceAspectRatio;
            if (declaredAspect <= 0f || actualAspect <= 0f || Mathf.Abs(actualAspect - declaredAspect) / declaredAspect > 0.01f)
                throw new InvalidDataException("focal-cover AssetManifest sourceAspectRatio does not match the resolved Sprite UV aspect.");
        }

        private static string ProjectAbsolutePath(string projectRelativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot) || string.IsNullOrWhiteSpace(projectRelativePath))
                return string.Empty;
            return Path.Combine(projectRoot, projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string AssetField(UiAsset asset, string field)
        {
            if (asset == null) return string.Empty;
            switch (field)
            {
                case "id": return asset.id;
                case "role": return asset.role;
                case "source": return asset.source;
                case "path": return asset.path;
                case "fallback": return asset.fallback;
                case "hash": return asset.hash;
                case "provenance": return asset.provenance;
                case "license": return asset.license;
                case "importPolicy": return asset.importPolicy;
                case "aspectPolicy": return asset.aspectPolicy;
                case "sourceAspectRatio": return asset.sourceAspectRatio > 0f ? asset.sourceAspectRatio.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
                case "atlasRotationPolicy": return asset.atlasRotationPolicy;
                case "cropPolicy": return asset.cropPolicy;
                case "nineSlice": return asset.nineSlice;
                case "atlasOwner": return asset.atlasOwner;
                case "focalPoint": return asset.focalPoint?.ToString(Formatting.None);
                case "resolutionSet": return asset.resolutionSet?.ToString(Formatting.None);
                default: return string.Empty;
            }
        }

        private static void ValidateAssetContract(UiSpec spec)
        {
            JObject policy = spec == null || spec.qualityGates == null ? null : spec.qualityGates["assetPolicy"] as JObject;
            bool productionReady = policy?.Value<bool?>("productionReady") == true;
            JArray acceptedSources = policy?["acceptedSources"] as JArray;
            JArray requiredFields = policy?["requiredFields"] as JArray;
            foreach (UiAsset asset in spec?.assets ?? Array.Empty<UiAsset>())
            {
                if (asset == null) continue;
                if (productionReady)
                {
                    if (acceptedSources == null || !acceptedSources.Any(item => string.Equals(item?.Value<string>(), asset.source, StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidDataException("production-ready asset source is not accepted: " + asset.id);
                    foreach (JToken required in requiredFields ?? new JArray())
                    {
                        string field = required.Value<string>();
                        if (string.IsNullOrWhiteSpace(field) || string.IsNullOrWhiteSpace(AssetField(asset, field)))
                            throw new InvalidDataException("production-ready asset field is missing: " + asset.id + "." + field);
                    }
                }

                // A declared asset path is authoritative for every image source. Generated
                // procedural art is a fallback only when the authored path cannot be loaded;
                // otherwise semantic IDs such as "season-hero" silently lose their real
                // texture because the procedural key is not the same as the asset ID.
                if (!string.IsNullOrWhiteSpace(asset.path))
                {
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(asset.path.Replace('\\', '/'));
                    if (productionReady && sprite == null)
                        throw new InvalidDataException("resolved asset is not an imported Sprite: " + asset.path);
                    string absolutePath = ProjectAbsolutePath(asset.path);
                    if (!string.IsNullOrWhiteSpace(asset.hash) && File.Exists(absolutePath))
                    {
                        string actualHash = ComputeSha256(absolutePath);
                        if (!string.Equals(actualHash, asset.hash, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("asset hash mismatch: " + asset.id);
                    }
                }
            }
        }

        private static bool HasDeclaredAsset(UiSpec spec, string id)
        {
            return (spec?.assets ?? Array.Empty<UiAsset>())
                .Any(asset => asset != null && string.Equals(asset.id, id, StringComparison.OrdinalIgnoreCase));
        }

        private static Sprite ResolveAssetSprite(string[] slots, UiSpec spec)
        {
            foreach (string slot in slots ?? Array.Empty<string>())
            {
                UiAsset asset = (spec?.assets ?? Array.Empty<UiAsset>())
                    .FirstOrDefault(item => item != null && string.Equals(item.id, slot, StringComparison.OrdinalIgnoreCase));
                if (asset == null) continue;
                string source = (asset.source ?? string.Empty).Trim().ToLowerInvariant();
                if (!string.IsNullOrWhiteSpace(asset.path))
                {
                    Sprite declaredSprite = AssetDatabase.LoadAssetAtPath<Sprite>(asset.path.Replace('\\', '/'));
                    if (declaredSprite != null) return declaredSprite;
                }
                if (source == "generated-procedural" || source == "ai-generated")
                    return GetProceduralAssetSprite(asset.id);
            }
            return GetGeneratedUiSprite();
        }

        private static Sprite GetProceduralAssetSprite(string id)
        {
            string key = string.IsNullOrWhiteSpace(id) ? "surface" : id.Trim().ToLowerInvariant();
            string cacheKey = key + "@" + MaterializerBuildId;
            if (ProceduralAssetCache.TryGetValue(cacheKey, out Sprite cached) && cached != null)
            {
                Debug.Log($"[ESUIGameScreenMaterializer] Procedural asset cache hit: {key}");
                return cached;
            }
            string assetPath = "Assets/UI/Generated/AIUI/" + key + "-" + MaterializerBuildId + ".asset";
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (existing != null)
            {
                ProceduralAssetCache[cacheKey] = existing;
                Debug.Log($"[ESUIGameScreenMaterializer] Procedural asset loaded: {assetPath}");
                return existing;
            }
            EnsureParentFolder(assetPath);
            int width = key.Contains("hero") || key.Contains("background") ? 1024 : key.Contains("frame") ? 512 : 256;
            int height = key.Contains("hero") || key.Contains("background") ? 512 : key.Contains("frame") ? 256 : 256;
            Texture2D texture = new Texture2D(width, height, UnityEngine.TextureFormat.RGBA32, 1, true)
            {
                name = key + "_GeneratedTexture",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            bool assetCommitted = false;
            Sprite sprite = null;
            try
            {
                Color32[] pixels = new Color32[width * height];
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float u = x / Mathf.Max(1f, width - 1f);
                    float v = y / Mathf.Max(1f, height - 1f);
                    pixels[y * width + x] = ProceduralAssetPixel(key, u, v);
                }
                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                AssetDatabase.CreateAsset(texture, assetPath);
                assetCommitted = true;

                sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
                if (sprite == null)
                    throw new InvalidOperationException("程序化 UI Sprite 创建失败：" + key);
                sprite.name = key + "_GeneratedSprite";
                AssetDatabase.AddObjectToAsset(sprite, texture);
                AssetDatabase.SaveAssetIfDirty(texture);
            }
            catch
            {
                if (sprite != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(sprite)))
                    UnityEngine.Object.DestroyImmediate(sprite);
                if (assetCommitted && AssetDatabase.LoadMainAssetAtPath(assetPath) == texture)
                    AssetDatabase.DeleteAsset(assetPath);
                else if (texture != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(texture)))
                    UnityEngine.Object.DestroyImmediate(texture);
                throw;
            }
            ProceduralAssetCache[cacheKey] = sprite;
            Debug.Log($"[ESUIGameScreenMaterializer] Procedural asset created: {assetPath} ({width}x{height})");
            return sprite;
        }

        private static float EllipseMask(Vector2 point, Vector2 center, float radiusX, float radiusY)
        {
            float dx = (point.x - center.x) / Mathf.Max(0.0001f, radiusX);
            float dy = (point.y - center.y) / Mathf.Max(0.0001f, radiusY);
            return Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
        }

        private static float SegmentMask(Vector2 point, Vector2 start, Vector2 end, float radius)
        {
            Vector2 segment = end - start;
            float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / Mathf.Max(0.0001f, segment.sqrMagnitude));
            return Mathf.Clamp01(1f - Vector2.Distance(point, start + segment * t) / Mathf.Max(0.0001f, radius));
        }

        private static Color32 ProceduralAssetPixel(string key, float u, float v)
        {
            Color top = new Color(0.035f, 0.055f, 0.11f, 1f);
            Color bottom = new Color(0.015f, 0.018f, 0.045f, 1f);
            if (key.Contains("hero"))
            {
                // v6 keeps a clear focal subject after texture import and Canvas
                // scaling: a bright armored champion, a warm armor plate, a
                // luminous horizon and a high-contrast energy slash.
                Color baseColor = Color.Lerp(new Color(0.02f, 0.07f, 0.20f), new Color(0.08f, 0.42f, 0.66f), v);
                float nebula = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v), new Vector2(0.74f, 0.30f)) * 1.65f);
                Color color = Color.Lerp(baseColor, new Color(0.38f, 0.08f, 0.34f), nebula * 0.48f);
                float horizon = Mathf.Clamp01(1f - Mathf.Abs(v - (0.78f + 0.035f * Mathf.Sin(u * 18f))) * 75f);
                color += new Color(0.18f, 0.58f, 0.86f) * horizon * 0.88f;
                float halo = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v), new Vector2(0.69f, 0.45f)) * 2.8f);
                color += new Color(0.08f, 0.25f, 0.48f) * halo * 0.72f;

                Vector2 hero = new Vector2(0.69f, 0.53f);
                // Texture pixels are authored bottom-to-top while the fixture is
                // reviewed top-to-bottom. Keep the champion upright in the
                // captured PNG instead of producing an inverted radial shape.
                Vector2 point = new Vector2(u, 1f - v);
                float head = EllipseMask(point, hero + new Vector2(0f, -0.25f), 0.095f, 0.11f);
                float neck = EllipseMask(point, hero + new Vector2(0f, -0.13f), 0.075f, 0.07f);
                float torso = EllipseMask(point, hero + new Vector2(0f, 0.06f), 0.17f, 0.23f);
                float shoulder = EllipseMask(point, hero + new Vector2(0f, -0.01f), 0.28f, 0.10f);
                float armLeft = SegmentMask(point, hero + new Vector2(-0.18f, -0.02f), hero + new Vector2(-0.31f, 0.29f), 0.055f);
                float armRight = SegmentMask(point, hero + new Vector2(0.18f, -0.02f), hero + new Vector2(0.30f, 0.27f), 0.055f);
                float legLeft = SegmentMask(point, hero + new Vector2(-0.08f, 0.20f), hero + new Vector2(-0.13f, 0.43f), 0.065f);
                float legRight = SegmentMask(point, hero + new Vector2(0.08f, 0.20f), hero + new Vector2(0.14f, 0.43f), 0.065f);
                float cape = Mathf.Clamp01(1f - Mathf.Abs(u - (hero.x - 0.14f - (v - 0.02f) * 0.22f)) / 0.17f) * Mathf.Clamp01((v - 0.02f) * 3.2f) * Mathf.Clamp01((0.40f - v) * 2.5f);
                float silhouette = Mathf.Clamp01(Mathf.Max(head, Mathf.Max(neck, Mathf.Max(torso, Mathf.Max(shoulder, Mathf.Max(cape, Mathf.Max(armLeft, Mathf.Max(armRight, Mathf.Max(legLeft, legRight)))))))));
                Color armor = Color.Lerp(new Color(0.07f, 0.12f, 0.21f), new Color(0.34f, 0.72f, 0.88f), Mathf.Clamp01(silhouette * 1.22f));
                color = Color.Lerp(color, armor, silhouette * 0.74f);
                float armorEdge = Mathf.Clamp01(Mathf.Abs(silhouette - 0.42f) * 13f) * silhouette;
                color += new Color(0.32f, 0.88f, 1.0f) * armorEdge * 0.95f;
                float goldPlate = EllipseMask(point, hero + new Vector2(-0.11f, 0.01f), 0.12f, 0.075f) * shoulder;
                color += new Color(1.0f, 0.56f, 0.16f) * goldPlate * 0.58f;
                Vector2 visorCenter = hero + new Vector2(0f, -0.23f);
                float visor = Mathf.Clamp01(1f - Vector2.Distance(point, visorCenter) * 42f);
                color += new Color(0.86f, 1f, 1f) * visor * 0.98f;
                float chest = EllipseMask(point, hero + new Vector2(0f, 0.04f), 0.075f, 0.10f);
                color += new Color(1.0f, 0.72f, 0.25f) * chest * 0.32f;

                Vector2 slashStart = new Vector2(0.44f, 0.88f);
                Vector2 slashEnd = new Vector2(0.90f, 0.16f);
                Vector2 segment = slashEnd - slashStart;
                float t = Mathf.Clamp01(Vector2.Dot(new Vector2(u, v) - slashStart, segment) / Mathf.Max(0.0001f, segment.sqrMagnitude));
                float slashDistance = Vector2.Distance(new Vector2(u, v), slashStart + segment * t);
                float slash = Mathf.Clamp01(1f - slashDistance * 95f) * Mathf.Clamp01(1f - Mathf.Abs(t - 0.5f) * 1.6f);
                color += new Color(0.44f, 0.96f, 1f) * slash * 1.22f;

                float star = Mathf.Pow(Mathf.Clamp01(Mathf.Sin(u * 173f) * Mathf.Sin(v * 97f) * 0.5f + 0.5f), 24f);
                color += new Color(0.36f, 0.74f, 1f) * star * (1f - silhouette) * 0.7f;
                return color;
            }
            if (key.Contains("background"))
            {
                Color color = Color.Lerp(bottom, top, v);
                float halo = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v), new Vector2(0.80f, 0.14f)) * 2.1f);
                color = Color.Lerp(color, new Color(0.10f, 0.20f, 0.34f), halo * 0.45f);
                float stars = Mathf.Pow(Mathf.Clamp01(Mathf.Sin(u * 151f) * Mathf.Sin(v * 83f) * 0.5f + 0.5f), 18f);
                return color + new Color(0.10f, 0.42f, 0.62f) * stars;
            }
            if (key.Contains("avatar"))
            {
                float d = Vector2.Distance(new Vector2(u, v), Vector2.one * 0.5f);
                Color color = Color.Lerp(new Color(0.12f, 0.38f, 0.52f), new Color(0.40f, 0.17f, 0.50f), v);
                float ring = Mathf.Clamp01(1f - Mathf.Abs(d - 0.38f) * 22f);
                float core = Mathf.Clamp01(1f - d * 3.2f);
                color = Color.Lerp(color, new Color(0.75f, 0.88f, 1f), core * 0.22f + ring * 0.34f);
                if (d > 0.48f) color.a = 0f;
                return color;
            }
            if (key.Contains("rank"))
            {
                float d = Mathf.Abs(u - 0.5f) + Mathf.Abs(v - 0.5f);
                Color color = Color.Lerp(new Color(0.18f, 0.08f, 0.02f), new Color(1f, 0.68f, 0.20f), Mathf.Clamp01(1f - d * 1.45f));
                if (d > 0.72f) color.a = 0f;
                return color;
            }
            if (key.Contains("mail-icon"))
            {
                float border = Mathf.Max(Mathf.Abs(u - 0.5f) - 0.34f, Mathf.Abs(v - 0.5f) - 0.22f);
                float envelope = Mathf.Clamp01(1f - Mathf.Abs(border) * 80f);
                float fold = Mathf.Clamp01(1f - Mathf.Abs(v - (0.50f + Mathf.Abs(u - 0.5f) * 0.55f)) * 80f);
                float fold2 = Mathf.Clamp01(1f - Mathf.Abs(v - (0.50f - Mathf.Abs(u - 0.5f) * 0.55f)) * 80f);
                float alpha = Mathf.Max(envelope, Mathf.Max(fold, fold2));
                return new Color(0.82f, 0.96f, 1f, alpha);
            }
            if (key.Contains("settings-icon"))
            {
                Vector2 center = new Vector2(0.5f, 0.5f);
                float distance = Vector2.Distance(new Vector2(u, v), center);
                float ring = Mathf.Clamp01(1f - Mathf.Abs(distance - 0.27f) * 70f);
                float hole = Mathf.Clamp01(1f - distance * 28f);
                float spokes = 0f;
                for (int spoke = 0; spoke < 8; spoke++)
                {
                    float angle = spoke * Mathf.PI / 4f;
                    Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.38f;
                    spokes = Mathf.Max(spokes, Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v), point) * 38f));
                }
                float alpha = Mathf.Max(ring, spokes) * (1f - hole * 0.8f);
                return new Color(0.82f, 0.96f, 1f, alpha);
            }
            if (key.Contains("mode"))
            {
                Color color = Color.Lerp(new Color(0.04f, 0.12f, 0.22f), new Color(0.12f, 0.44f, 0.56f), v);
                float flare = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v), new Vector2(0.72f, 0.28f)) * 2.8f);
                return Color.Lerp(color, new Color(0.20f, 0.80f, 0.92f), flare * 0.5f);
            }
            Color surface = Color.Lerp(new Color(0.045f, 0.09f, 0.16f, 0.98f), new Color(0.075f, 0.16f, 0.24f, 0.98f), v);
            float edge = Mathf.Clamp01(Mathf.Min(Mathf.Min(u, v), Mathf.Min(1f - u, 1f - v)) * 16f);
            return Color.Lerp(new Color(0.08f, 0.28f, 0.38f, 1f), surface, edge);
        }

        private static Sprite GetGeneratedUiSprite()
        {
            const string texturePath = "Assets/UI/Generated/ESUI_WhiteTexture.asset";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
            if (sprite != null) return sprite;
            string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Assets/UI/Generated");
            Directory.CreateDirectory(folder);
            Texture2D existingTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (existingTexture != null)
            {
                sprite = Sprite.Create(existingTexture, new Rect(0, 0, existingTexture.width, existingTexture.height), new Vector2(0.5f, 0.5f), 100f);
                sprite.name = "ESUI_WhiteSprite";
                try
                {
                    AssetDatabase.AddObjectToAsset(sprite, existingTexture);
                    AssetDatabase.SaveAssetIfDirty(existingTexture);
                    return sprite;
                }
                catch
                {
                    if (sprite != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(sprite)))
                        UnityEngine.Object.DestroyImmediate(sprite);
                    throw;
                }
            }
            UnityEngine.Texture2D texture = new UnityEngine.Texture2D(2, 2, UnityEngine.TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                texture.Apply();
                texture.name = "ESUI_WhiteTexture";
                AssetDatabase.CreateAsset(texture, texturePath);
            }
            catch
            {
                if (texture != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(texture)))
                    UnityEngine.Object.DestroyImmediate(texture);
                throw;
            }
            sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f); sprite.name = "ESUI_WhiteSprite";
            try
            {
                AssetDatabase.AddObjectToAsset(sprite, texture);
                AssetDatabase.SaveAssetIfDirty(texture);
                return sprite;
            }
            catch
            {
                if (sprite != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(sprite)))
                    UnityEngine.Object.DestroyImmediate(sprite);
                if (AssetDatabase.LoadMainAssetAtPath(texturePath) == texture)
                    AssetDatabase.DeleteAsset(texturePath);
                else if (texture != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(texture)))
                    UnityEngine.Object.DestroyImmediate(texture);
                throw;
            }
        }

        private static TextMeshProUGUI CreateChildText(GameObject parent, string name, string value, float fontSize, Color color, float minX, float minY, float maxX, float maxY, TextAlignmentOptions alignment)
        {
            GameObject child = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); child.transform.SetParent(parent.transform, false);
            RectTransform rect = child.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(minX, minY); rect.anchorMax = new Vector2(maxX, maxY); rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            TextMeshProUGUI text = child.GetComponent<TextMeshProUGUI>(); ApplyShowcaseFont(text); text.text = value ?? string.Empty; text.fontSize = fontSize; text.color = color; text.alignment = alignment; text.enableWordWrapping = true; text.raycastTarget = false;
            return text;
        }

        private static void ConfigureFontForSpec(UiSpec spec)
        {
            ActiveFontAsset = null;
            ActiveFontMaterial = null;
            JObject typography = spec == null || spec.qualityGates == null ? null : spec.qualityGates["typographyPolicy"] as JObject;
            string fontPath = typography?.Value<string>("fontAssetPath");
            string declaredHash = typography?.Value<string>("fontAssetHash");
            TMP_FontAsset font = string.IsNullOrWhiteSpace(fontPath)
                ? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ShowcaseFontPath)
                : AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath.Replace('\\', '/'));
            if (font == null)
                throw new FileNotFoundException("ScreenSpec 声明的 TMP Font Asset 不存在。", fontPath ?? ShowcaseFontPath);
            if (!string.IsNullOrWhiteSpace(declaredHash))
            {
                string absolutePath = ProjectAbsolutePath(string.IsNullOrWhiteSpace(fontPath) ? ShowcaseFontPath : fontPath);
                if (!File.Exists(absolutePath))
                    throw new FileNotFoundException("ScreenSpec 字体哈希对应的文件不存在。", absolutePath);
                string actualHash = ComputeSha256(absolutePath);
                if (!string.Equals(actualHash, declaredHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("ScreenSpec 字体 Font Asset 哈希不匹配：" + fontPath);
            }

            // Resolve and install the declared fallback chain before checking glyphs.
            // An ID alone is not a usable fallback: every distinct asset must have a
            // project path and hash so a stale or empty TMP asset cannot pass silently.
            List<TMP_FontAsset> fallbackAssets = new List<TMP_FontAsset>();
            JArray fallbackIds = typography == null ? null : typography["fallbackFontAssetIds"] as JArray;
            JArray fallbackRecords = typography == null ? null : typography["fallbackFontAssets"] as JArray;
            if (fallbackIds != null)
            {
                foreach (JToken idToken in fallbackIds)
                {
                    string fallbackId = idToken?.Value<string>() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(fallbackId) || string.Equals(fallbackId, typography?.Value<string>("fontAssetId"), StringComparison.Ordinal))
                        continue;
                    JObject record = fallbackRecords?.OfType<JObject>().FirstOrDefault(item => string.Equals(item.Value<string>("id"), fallbackId, StringComparison.Ordinal));
                    if (record == null)
                        throw new InvalidDataException("ScreenSpec 缺少 distinct fallback 字体元数据：" + fallbackId);
                    string fallbackPath = record.Value<string>("path");
                    string fallbackHash = record.Value<string>("hash");
                    TMP_FontAsset fallback = string.IsNullOrWhiteSpace(fallbackPath)
                        ? null
                        : AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fallbackPath.Replace('\\', '/'));
                    if (fallback == null)
                        throw new FileNotFoundException("ScreenSpec 声明的 fallback TMP Font Asset 不存在。", fallbackPath);
                    if (string.IsNullOrWhiteSpace(fallbackHash))
                        throw new InvalidDataException("ScreenSpec fallback 字体哈希缺失：" + fallbackId);
                    string fallbackAbsolutePath = ProjectAbsolutePath(fallbackPath);
                    if (!File.Exists(fallbackAbsolutePath) || !string.Equals(ComputeSha256(fallbackAbsolutePath), fallbackHash, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("ScreenSpec fallback 字体哈希不匹配：" + fallbackPath);
                    if (fallback != font && !fallbackAssets.Contains(fallback)) fallbackAssets.Add(fallback);
                }
            }
            font.fallbackFontAssetTable = fallbackAssets;

            JArray requiredCharacters = typography == null ? null : typography["requiredCharacters"] as JArray;
            if (requiredCharacters != null && requiredCharacters.Count > 0 && font.characterLookupTable != null)
            {
                var missing = new List<string>();
                foreach (JToken token in requiredCharacters)
                {
                    string value = token?.Value<string>() ?? string.Empty;
                    foreach (char character in value)
                    {
                        bool covered = font.characterLookupTable.ContainsKey((uint)character)
                                       || fallbackAssets.Any(item => item != null && item.characterLookupTable != null && item.characterLookupTable.ContainsKey((uint)character));
                        if (!covered) missing.Add(character.ToString());
                    }
                }
                if (missing.Count > 0)
                    throw new InvalidDataException("ScreenSpec 字体缺少必需字形：" + string.Join(string.Empty, missing.Distinct()));
            }

            ActiveFontAsset = font;
            ActiveFontMaterial = font.material;
            if (ActiveFontMaterial == null)
            {
                string materialPath = Path.ChangeExtension(string.IsNullOrWhiteSpace(fontPath) ? ShowcaseFontPath : fontPath, ".mat").Replace('\\', '/');
                ActiveFontMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            }
        }

        private static void ApplyShowcaseFont(TextMeshProUGUI text)
        {
            if (text == null)
                return;
            TMP_FontAsset font = ActiveFontAsset ?? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ShowcaseFontPath);
            Material material = ActiveFontMaterial ?? AssetDatabase.LoadAssetAtPath<Material>(ShowcaseFontMaterialPath);
            if (font != null)
                text.font = font;
            if (material != null)
                text.fontSharedMaterial = material;
        }

        private static Text CreateLegacyChildText(GameObject parent, string name, string value, float fontSize, Color color, float minX, float minY, float maxX, float maxY)
        {
            GameObject child = new GameObject(name, typeof(RectTransform), typeof(Text)); child.transform.SetParent(parent.transform, false);
            RectTransform rect = child.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(minX, minY); rect.anchorMax = new Vector2(maxX, maxY); rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            Text text = child.GetComponent<Text>(); text.text = value ?? string.Empty; text.fontSize = Mathf.RoundToInt(fontSize); text.color = color; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            return text;
        }

        private static void ConfigureContainer(GameObject go, UiContainerLayout layout)
        {
            if (go == null || layout == null) return;
            string mode = (layout.layoutMode ?? "absolute").ToLowerInvariant();
            RectOffset padding = new RectOffset(Mathf.RoundToInt(layout.paddingLeft), Mathf.RoundToInt(layout.paddingRight),
                Mathf.RoundToInt(layout.paddingTop), Mathf.RoundToInt(layout.paddingBottom));
            TextAnchor alignment = ParseChildAlignment(layout.childAlignment);
            if (mode == "vertical")
            {
                VerticalLayoutGroup group = go.AddComponent<VerticalLayoutGroup>();
                group.padding = padding; group.spacing = layout.gap; group.childAlignment = alignment;
                group.childControlWidth = layout.controlChildWidth; group.childControlHeight = layout.controlChildHeight;
                group.childForceExpandWidth = layout.forceChildExpandWidth; group.childForceExpandHeight = layout.forceChildExpandHeight;
            }
            else if (mode == "horizontal")
            {
                HorizontalLayoutGroup group = go.AddComponent<HorizontalLayoutGroup>();
                group.padding = padding; group.spacing = layout.gap; group.childAlignment = alignment;
                group.childControlWidth = layout.controlChildWidth; group.childControlHeight = layout.controlChildHeight;
                group.childForceExpandWidth = layout.forceChildExpandWidth; group.childForceExpandHeight = layout.forceChildExpandHeight;
            }
            else if (mode == "grid")
            {
                GridLayoutGroup group = go.AddComponent<GridLayoutGroup>();
                group.padding = padding; group.spacing = new Vector2(layout.spacingX, layout.spacingY);
                group.childAlignment = alignment; group.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                group.constraintCount = Mathf.Max(1, layout.columns);
                group.cellSize = new Vector2(Mathf.Max(1f, layout.cellWidth), Mathf.Max(1f, layout.cellHeight));
            }
        }

        private static TextAnchor ParseChildAlignment(string value)
        {
            switch ((value ?? string.Empty).ToLowerInvariant())
            {
                case "upper-center": return TextAnchor.UpperCenter;
                case "upper-right": return TextAnchor.UpperRight;
                case "middle-left": return TextAnchor.MiddleLeft;
                case "middle-center": return TextAnchor.MiddleCenter;
                case "middle-right": return TextAnchor.MiddleRight;
                case "lower-left": return TextAnchor.LowerLeft;
                case "lower-center": return TextAnchor.LowerCenter;
                case "lower-right": return TextAnchor.LowerRight;
                default: return TextAnchor.UpperLeft;
            }
        }

        private static void ApplyLayoutIntent(RectTransform rect, LayoutElement size, UiLayoutIntent intent, bool managedByParent = false)
        {
            if (intent == null) return;
            // ScreenSpec bounds are authored in top-left coordinates so the AI can
            // reason about screenshots directly. UGUI anchors use a bottom-left
            // origin; mirror only the vertical axis at this boundary.
            rect.anchorMin = new Vector2(intent.anchorMinX, 1f - intent.anchorMaxY);
            rect.anchorMax = new Vector2(intent.anchorMaxX, 1f - intent.anchorMinY);
            rect.pivot = new Vector2(intent.pivotX, 1f - intent.pivotY);
            rect.anchoredPosition = new Vector2(intent.anchoredX, intent.anchoredY);
            if (intent.sizeWidth > 0f || intent.sizeHeight > 0f)
                rect.sizeDelta = new Vector2(intent.sizeWidth, intent.sizeHeight);
            else
                rect.sizeDelta = Vector2.zero;
            // A parent Flow/Grid solver owns managed children; otherwise explicit AI geometry owns the RectTransform.
            size.ignoreLayout = !managedByParent;
            if (managedByParent)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
            }
        }

        private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; }
        private static Color ParseToken(string token, UiTokens t, Color fallback) => ParseColor(token == "accent" ? t.accent : token == "surface" ? t.surface : token == "surfaceRaised" ? t.surfaceRaised : token == "mutedText" ? t.mutedText : token == "danger" ? t.danger : token == "background" ? t.background : token == "onAccent" ? t.onAccent : token == "onDanger" ? t.onDanger : token == "text" ? t.text : token, fallback);

        private static float ResolveFontSize(string role, string kind, UiTokens tokens)
        {
            string key = (role ?? string.Empty).Trim().ToLowerInvariant();
            if (key == "title") return tokens.titleSize;
            if (key == "label") return tokens.labelSize > 0 ? tokens.labelSize : tokens.buttonSize;
            if (key == "caption") return tokens.captionSize > 0 ? tokens.captionSize : Mathf.Max(12, tokens.bodySize - 4);
            if (key == "numeric") return tokens.numericSize > 0 ? tokens.numericSize : tokens.bodySize;
            return kind == "title" ? tokens.titleSize : tokens.bodySize;
        }

        private static Color ParseVisualVariant(string variant, UiTokens tokens, Color fallback)
        {
            string key = (variant ?? string.Empty).Trim().ToLowerInvariant();
            if (key == "accent" || key == "primary" || key == "selected") return ParseColor(tokens.accent, fallback);
            if (key == "surface" || key == "panel" || key == "card") return ParseColor(tokens.surface, fallback);
            if (key == "background") return ParseColor(tokens.background, fallback);
            if (key == "mutedtext" || key == "muted") return ParseColor(tokens.mutedText, fallback);
            if (key == "danger" || key == "error" || key == "feedback") return ParseColor(tokens.danger, fallback);
            if (key == "text") return ParseColor(tokens.text, fallback);
            return fallback;
        }
        private static Color ParseTextOnVariant(string variant, UiTokens tokens, Color fallback)
        {
            string key = (variant ?? string.Empty).Trim().ToLowerInvariant();
            if (key == "accent" || key == "primary" || key == "selected") return ParseColor(tokens.onAccent, fallback);
            if (key == "danger" || key == "error" || key == "feedback") return ParseColor(tokens.onDanger, fallback);
            return ParseColor(tokens.text, fallback);
        }
        private static Color ParseColor(string value, Color fallback) { if (string.IsNullOrWhiteSpace(value)) return fallback; return ColorUtility.TryParseHtmlString(value, out Color c) ? c : fallback; }

        private static void EnsureShowcaseFontCharacters(UiSpec spec)
        {
            TMP_FontAsset font = ActiveFontAsset ?? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ShowcaseFontPath);
            if (font == null || spec == null) return;
            var characters = new StringBuilder();
            foreach (UiElement element in spec.elements ?? Array.Empty<UiElement>())
                AppendUiText(element, characters);
            if (characters.Length == 0) return;

            // Keep the shared ES font as the single visual authority, but allow the
            // materializer to add glyphs required by a newly authored screen.
            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            font.TryAddCharacters(characters.ToString(), out string missingCharacters);
            font.ReadFontAssetDefinition();
            EditorUtility.SetDirty(font);
            if (!string.IsNullOrEmpty(missingCharacters))
                Debug.LogWarning("[ESUIGameScreenMaterializer] UI 字体仍缺少 glyph：" + missingCharacters);
            AssetDatabase.SaveAssetIfDirty(font);
        }

        private static void AppendUiText(UiElement element, StringBuilder target)
        {
            if (element == null) return;
            if (!string.IsNullOrEmpty(element.text)) target.Append(element.text);
            foreach (UiElement child in element.children ?? Array.Empty<UiElement>())
                AppendUiText(child, target);
        }
        private static void EnsureParentFolder(string assetPath) { string dir = Path.GetDirectoryName(assetPath)?.Replace('\\', '/'); if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(Path.Combine(Directory.GetParent(Application.dataPath).FullName, dir)); }
        private static void EnsureEventSystemInScene() { new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule)); }

        private static string CaptureFixture(UiSpec spec, string panelId, Camera camera, RectTransform canvasRect, Transform instance,
            int width, int height, string profile, string state, string contractSha256, string specHash, string runId, int sceneGeneration,
            string evidenceRoot, FixtureStateBaseline baseline)
        {
            string root = string.IsNullOrWhiteSpace(evidenceRoot)
                ? Path.Combine(Directory.GetParent(Application.dataPath).FullName, "ES", "UIEvidence", panelId)
                : ESAutomationPathPolicy.Normalize(evidenceRoot);
            string evidenceBase = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "ES", "UIEvidence");
            if (!ESAutomationPathPolicy.IsWithin(root, new[] { evidenceBase }))
                throw new InvalidDataException("UI evidenceRoot 必须位于 ES/UIEvidence 内。");
            Directory.CreateDirectory(root);
            RenderTexture target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            Texture2D image = null;
            try
            {
                EnsureFixtureCanvasGeometry(canvasRect, width, height);
                ApplyFixtureProfile(instance.gameObject, profile);
                ConfigureFixtureInstanceCanvas(instance.gameObject, camera, width, height);
                camera.aspect = width / (float)height;
                camera.orthographicSize = height * 0.5f;
                camera.transform.position = new Vector3(width * 0.5f, height * 0.5f, -1000f);
                camera.targetTexture = target;
                camera.enabled = true;
                Canvas.ForceUpdateCanvases();
                EnsureFixtureCanvasGeometry(canvasRect, width, height);
                ConfigureFixtureInstanceCanvas(instance.gameObject, camera, width, height);
                ApplyFixtureProfile(instance.gameObject, profile);
                ReapplySpecGeometry(instance.gameObject, spec);
                Canvas.ForceUpdateCanvases();
                EnsureFixtureCanvasGeometry(canvasRect, width, height);
                ConfigureFixtureInstanceCanvas(instance.gameObject, camera, width, height);
                ReapplySpecGeometry(instance.gameObject, spec);
                // Canvas dimension callbacks can re-run ESUIAdaptiveLayout. The fixture
                // matrix is authoritative for capture, so apply the requested profile
                // after the final geometry pass and rebuild layout groups before rendering.
                ApplyFixtureProfile(instance.gameObject, profile);
                RebuildLayoutTree(instance.transform as RectTransform);
                Canvas.ForceUpdateCanvases();
                RebuildLayoutTree(instance.transform as RectTransform);
                RepairDeterministicContainers(instance.transform as RectTransform);
                // Profile/geometry repair may invoke Selectable and layout callbacks that
                // overwrite fixture colors, interactability or state-only visuals. Reapply
                // the requested state at the final render boundary so PNG and snapshots
                // describe the same deterministic fixture.
                ApplyFixtureState(instance.gameObject, state, spec, baseline);
                Canvas.ForceUpdateCanvases();
                RenderTexture.active = target;
                GL.Clear(true, true, camera.backgroundColor);
                camera.Render();
                image = new Texture2D(width, height, UnityEngine.TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                image.Apply();
                string outputPath = Path.Combine(root, profile + "__" + state + ".png");
                byte[] pngBytes = image.EncodeToPNG();
                ES.ESManagedFileIO.WriteBytesAtomic(outputPath, pngBytes, root);
                JObject capture = BuildPngCaptureMetadata(image, outputPath, pngBytes.Length);
                WriteEvidenceSnapshots(root, panelId, profile, state, width, height, instance,
                    canvasRect, contractSha256, specHash, runId, sceneGeneration, spec, capture);
                return outputPath.Replace('\\', '/');
            }
            finally
            {
                if (image != null)
                    UnityEngine.Object.DestroyImmediate(image);
                if (camera != null)
                    camera.targetTexture = null;
                RenderTexture.active = previous;
                if (target != null)
                {
                    target.Release();
                    UnityEngine.Object.DestroyImmediate(target);
                }
            }
        }

        private static void EnsureFixtureCanvasGeometry(RectTransform canvasRect, int width, int height)
        {
            if (canvasRect == null) return;
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.zero;
            canvasRect.pivot = Vector2.zero;
            canvasRect.anchoredPosition = Vector2.zero;
            canvasRect.sizeDelta = new Vector2(width, height);
            canvasRect.position = new Vector3(width * 0.5f, height * 0.5f, 0f);
            canvasRect.localScale = Vector3.one;
        }

        private static void ConfigureFixtureInstanceCanvas(GameObject instance, Camera camera, int width, int height)
        {
            if (instance == null) return;
            RectTransform rect = instance.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = Vector2.zero;
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(width, height);
                rect.localScale = Vector3.one;
            }
            Canvas canvas = instance.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1000f;
            }
        }

        private static void RebuildLayoutTree(RectTransform root)
        {
            if (root == null) return;
            for (int i = 0; i < root.childCount; i++)
            {
                RectTransform child = root.GetChild(i) as RectTransform;
                if (child != null) RebuildLayoutTree(child);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
        }

        private static void RepairDeterministicContainers(RectTransform root)
        {
            if (root == null) return;
            foreach (ESUIComponentSemantic semantic in root.GetComponentsInChildren<ESUIComponentSemantic>(true))
            {
                if (semantic == null) continue;
                RectTransform container = semantic.transform as RectTransform;
                if (container == null) continue;
                string type = (semantic.componentType ?? string.Empty).ToLowerInvariant();
                if (type == "tab-bar") RepairTabBar(container);
                else if (type == "list") RepairScrollList(container);
            }
        }

        private static void RepairTabBar(RectTransform container)
        {
            HorizontalLayoutGroup group = container.GetComponent<HorizontalLayoutGroup>();
            if (group == null) return;
            float width = Mathf.Max(1f, container.rect.width);
            float height = Mathf.Max(1f, container.rect.height);
            int count = container.childCount;
            if (count == 0) return;
            float left = group.padding.left;
            float top = group.padding.top;
            float itemWidth = Mathf.Max(1f, (width - left - group.padding.right - group.spacing * (count - 1)) / count);
            float itemHeight = Mathf.Min(64f, Mathf.Max(24f, height - top - group.padding.bottom));
            for (int i = 0; i < count; i++)
            {
                RectTransform child = container.GetChild(i) as RectTransform;
                if (child == null || !child.gameObject.activeSelf) continue;
                child.anchorMin = new Vector2(0f, 1f);
                child.anchorMax = new Vector2(0f, 1f);
                child.pivot = new Vector2(0f, 1f);
                child.sizeDelta = new Vector2(itemWidth, itemHeight);
                child.anchoredPosition = new Vector2(left + i * (itemWidth + group.spacing), -top);
            }
            // The authored bounds describe the placement region, but a tab bar's
            // visual surface must not retain a taller empty tail after its items
            // are clamped to the interaction height. Keep the bottom edge stable
            // and compact the top edge to the actual button row.
            RectTransform parent = container.parent as RectTransform;
            if (parent != null && parent.rect.height > 1f)
            {
                float compactHeight = itemHeight + group.padding.top + group.padding.bottom;
                float normalizedHeight = Mathf.Clamp01(compactHeight / parent.rect.height);
                float minY = container.anchorMin.y;
                float compactMaxY = Mathf.Min(1f, minY + normalizedHeight);
                if (compactMaxY > minY + 0.0001f && compactMaxY < container.anchorMax.y - 0.0001f)
                {
                    container.anchorMax = new Vector2(container.anchorMax.x, compactMaxY);
                    container.offsetMax = new Vector2(container.offsetMax.x, 0f);
                }
            }
            group.enabled = false;
        }

        private static void RepairScrollList(RectTransform container)
        {
            ScrollRect scroll = container.GetComponent<ScrollRect>();
            RectTransform content = scroll == null ? null : scroll.content;
            if (content == null) return;
            HorizontalLayoutGroup horizontalGroup = content.GetComponent<HorizontalLayoutGroup>();
            if (horizontalGroup != null)
            {
                float cursor = horizontalGroup.padding.left;
                float maxHeight = Mathf.Max(1f, content.rect.height);
                float itemHeight = Mathf.Max(1f, maxHeight - horizontalGroup.padding.top - horizontalGroup.padding.bottom);
                for (int i = 0; i < content.childCount; i++)
                {
                    RectTransform child = content.GetChild(i) as RectTransform;
                    if (child == null || !child.gameObject.activeSelf) continue;
                    LayoutElement layout = child.GetComponent<LayoutElement>();
                    float childWidth = layout == null ? 180f : Mathf.Max(120f, layout.preferredWidth, layout.minWidth);
                    child.anchorMin = new Vector2(0f, 0f);
                    child.anchorMax = new Vector2(0f, 0f);
                    child.pivot = new Vector2(0f, 0f);
                    child.sizeDelta = new Vector2(childWidth, itemHeight);
                    child.anchoredPosition = new Vector2(cursor, horizontalGroup.padding.bottom);
                    cursor += childWidth + horizontalGroup.spacing;
                }
                content.anchorMin = new Vector2(0f, 0f);
                content.anchorMax = new Vector2(0f, 1f);
                content.pivot = new Vector2(0f, 0.5f);
                content.sizeDelta = new Vector2(Mathf.Max(content.rect.width, cursor - horizontalGroup.spacing + horizontalGroup.padding.right), 0f);
                horizontalGroup.enabled = false;
                ContentSizeFitter horizontalFitter = content.GetComponent<ContentSizeFitter>();
                if (horizontalFitter != null) horizontalFitter.enabled = false;
                return;
            }
            VerticalLayoutGroup group = content.GetComponent<VerticalLayoutGroup>();
            if (group == null) return;
            float verticalCursor = -group.padding.top;
            float maxWidth = Mathf.Max(1f, content.rect.width);
            for (int i = 0; i < content.childCount; i++)
            {
                RectTransform child = content.GetChild(i) as RectTransform;
                if (child == null || !child.gameObject.activeSelf) continue;
                LayoutElement layout = child.GetComponent<LayoutElement>();
                float childHeight = layout == null ? 64f : Mathf.Max(64f, layout.preferredHeight, layout.minHeight);
                child.anchorMin = new Vector2(0f, 1f);
                child.anchorMax = new Vector2(0f, 1f);
                child.pivot = new Vector2(0f, 1f);
                child.sizeDelta = new Vector2(maxWidth, childHeight);
                child.anchoredPosition = new Vector2(0f, verticalCursor);
                verticalCursor -= childHeight + group.spacing;
            }
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, Mathf.Max(content.rect.height, -verticalCursor + group.padding.bottom));
            group.enabled = false;
            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter != null) fitter.enabled = false;
        }

        private static FixtureStateBaseline CaptureFixtureStateBaseline(GameObject instance)
        {
            FixtureStateBaseline baseline = new FixtureStateBaseline();
            if (instance == null) return baseline;
            foreach (Transform transform in instance.GetComponentsInChildren<Transform>(true))
                baseline.activeSelf[transform.gameObject] = transform.gameObject.activeSelf;
            foreach (Graphic graphic in instance.GetComponentsInChildren<Graphic>(true))
                if (graphic != null) baseline.graphicColors[graphic] = graphic.color;
            foreach (TMP_Text text in instance.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text == null) continue;
                baseline.wrapping[text] = text.enableWordWrapping;
                baseline.text[text] = text.text ?? string.Empty;
            }
            foreach (Button button in instance.GetComponentsInChildren<Button>(true))
                if (button != null) baseline.interactable[button] = button.interactable;
            foreach (Outline outline in instance.GetComponentsInChildren<Outline>(true))
                if (outline != null) baseline.outlines[outline] = outline.enabled;
            return baseline;
        }

        private static void ResetFixtureState(GameObject instance, FixtureStateBaseline baseline)
        {
            if (instance == null || baseline == null) return;
            foreach (KeyValuePair<GameObject, bool> pair in baseline.activeSelf)
                if (pair.Key != null) pair.Key.SetActive(pair.Value);
            foreach (KeyValuePair<Graphic, Color> pair in baseline.graphicColors)
                if (pair.Key != null) pair.Key.color = pair.Value;
            foreach (KeyValuePair<TMP_Text, bool> pair in baseline.wrapping)
                if (pair.Key != null) pair.Key.enableWordWrapping = pair.Value;
            foreach (KeyValuePair<TMP_Text, string> pair in baseline.text)
                if (pair.Key != null) pair.Key.text = pair.Value;
            foreach (KeyValuePair<Button, bool> pair in baseline.interactable)
                if (pair.Key != null) pair.Key.interactable = pair.Value;
            foreach (Outline outline in instance.GetComponentsInChildren<Outline>(true))
            {
                if (outline == null) continue;
                outline.enabled = baseline.outlines.TryGetValue(outline, out bool enabled) && enabled;
            }
            foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
                if (child.name == "__FixtureState") child.gameObject.SetActive(false);
        }

        private static bool StateTargets(UiSpec spec, string state, string componentId)
        {
            JObject semantics = spec?.stateSemantics == null ? null : spec.stateSemantics[state] as JObject;
            JArray affected = semantics == null ? null : semantics["affectedComponentIds"] as JArray;
            // Legacy packets have no semantic state map. Preserve their historical
            // fixture behavior; quality-gated packets must provide the map.
            if (affected == null || affected.Count == 0) return true;
            return affected.Values<string>().Any(value => string.Equals(value, componentId, StringComparison.Ordinal));
        }

        private static JToken StateSemantics(UiSpec spec, string state)
        {
            return spec == null || spec.stateSemantics == null ? null : spec.stateSemantics[state];
        }

        private static void ApplyFixtureState(GameObject instance, string state, UiSpec spec, FixtureStateBaseline baseline = null)
        {
            if (instance == null) return;
            bool selected = string.Equals(state, "selected", StringComparison.OrdinalIgnoreCase);
            bool disabled = string.Equals(state, "disabled", StringComparison.OrdinalIgnoreCase);
            bool empty = string.Equals(state, "empty", StringComparison.OrdinalIgnoreCase);
            bool loading = string.Equals(state, "loading", StringComparison.OrdinalIgnoreCase);
            bool error = string.Equals(state, "error", StringComparison.OrdinalIgnoreCase);
            Button selectedTarget = selected ? ResolveFixtureSelectionTarget(instance, spec, state) : null;
            foreach (ESUIComponentSemantic semantic in instance.GetComponentsInChildren<ESUIComponentSemantic>(true))
            {
                if (semantic == null) continue;
                bool targeted = StateTargets(spec, state, semantic.gameObject.name);
                Outline outline = semantic.GetComponent<Outline>();
                bool authoredSelected = string.Equals(semantic.visualVariant, "selected", StringComparison.OrdinalIgnoreCase);
                bool isSelectedTarget = selectedTarget != null && semantic.gameObject == selectedTarget.gameObject;
                if (outline != null) outline.enabled = authoredSelected || isSelectedTarget;
                string type = (semantic.componentType ?? string.Empty).ToLowerInvariant();
                bool hideData = targeted && empty && (type == "item-slot" || type == "item-card" || type == "list" || type == "grid");
                foreach (Transform child in semantic.transform)
                {
                    if (child.name == "Icon" || child.name == "Name" || child.name == "Rarity" || child.name == "Quantity") child.gameObject.SetActive(!hideData);
                    if (child.name == "State" && (loading || error || empty)) child.gameObject.SetActive(true);
                }
                if (targeted && loading && (type == "loading" || type == "progress" || type == "bar"))
                    SetGraphicAlpha(semantic.gameObject, 0.72f);
                else if (targeted && error && (type == "error-state" || type == "status-badge"))
                    SetGraphicColor(semantic.gameObject, ParseColor("#E86C73", Color.red));
                else if (targeted && disabled) SetGraphicAlpha(semantic.gameObject, 0.45f);
            }
            foreach (Button button in instance.GetComponentsInChildren<Button>(true))
            {
                Image image = button.GetComponent<Image>();
                if (image == null) continue;
                bool targeted = StateTargets(spec, state, button.gameObject.name);
                ColorBlock colors = button.colors;
                // The fixture owns the captured state because ColorTint is disabled above.
                bool isSelectedTarget = selectedTarget != null && button == selectedTarget;
                image.color = targeted && disabled
                    ? colors.disabledColor
                    : isSelectedTarget
                        ? Color.Lerp(colors.selectedColor, Color.white, 0.18f)
                        : colors.normalColor;
                if (targeted) button.interactable = !disabled;
                if (targeted && selected && isSelectedTarget)
                    EnsureFixtureSelectionOutline(button);
            }
            // Generic fixture affordances are compatibility behavior. A strict packet's
            // declarative effects run last so the captured pixels are traceable to its
            // state contract instead of a materializer heuristic.
            ApplyDeclaredStateEffects(instance, state, spec);
            ApplyFixtureTextBindings(instance, state, spec);
            // Strict ScreenSpec v3 captures already bind the active state in their
            // evidence snapshots. Do not paint an undeclared debug badge over a
            // high-fidelity screen; retain it only for legacy packets without a
            // semantic state contract.
            if (StateSemantics(spec, state) == null)
                ApplyFixtureStateBadge(instance, state);
        }

        private static Button ResolveFixtureSelectionTarget(GameObject instance, UiSpec spec, string state)
        {
            if (instance == null) return null;
            Button best = null;
            int bestScore = int.MinValue;
            // ScreenSpec v3 may materialize profile elements directly below the
            // instance (wide-*/narrow-*) rather than under Wide/Narrow wrappers.
            foreach (Button button in instance.GetComponentsInChildren<Button>(true))
            {
                if (button == null || !button.gameObject.activeInHierarchy || !StateTargets(spec, state, button.gameObject.name)) continue;
                ESUIComponentSemantic semantic = button.GetComponent<ESUIComponentSemantic>();
                string id = button.gameObject.name.ToLowerInvariant();
                string variant = semantic == null ? string.Empty : (semantic.visualVariant ?? string.Empty).ToLowerInvariant();
                int score = 10;
                if (variant == "selected") score += 100;
                if (id.Contains("ranked") || id.Contains("primary") || id.Contains("play") || id.Contains("home")) score += 60;
                if (id.Contains("settings") || id.Contains("mail") || id.Contains("invite")) score -= 25;
                if (score > bestScore)
                {
                    best = button;
                    bestScore = score;
                }
            }
            return best;
        }

        private static void EnsureFixtureSelectionOutline(Button button)
        {
            if (button == null) return;
            Outline outline = button.GetComponent<Outline>();
            if (outline == null) outline = button.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.88f, 0.48f, 0.98f);
            outline.effectDistance = new Vector2(3f, 3f);
            outline.useGraphicAlpha = true;
            outline.enabled = true;
        }

        private static void ApplyDeclaredStateEffects(GameObject instance, string state, UiSpec spec)
        {
            JObject semantics = StateSemantics(spec, state) as JObject;
            JArray effects = semantics == null ? null : semantics["effects"] as JArray;
            if (instance == null || effects == null || effects.Count == 0) return;

            Dictionary<string, ESUIComponentSemantic> components = instance
                .GetComponentsInChildren<ESUIComponentSemantic>(true)
                .Where(semantic => semantic != null)
                .GroupBy(semantic => semantic.gameObject.name, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            foreach (JObject effect in effects.OfType<JObject>())
            {
                string componentId = effect.Value<string>("componentId");
                JObject changes = effect["changes"] as JObject;
                if (string.IsNullOrWhiteSpace(componentId) || changes == null || !StateTargets(spec, state, componentId)) continue;
                if (!components.TryGetValue(componentId, out ESUIComponentSemantic semantic) || semantic == null) continue;
                ApplyDeclaredStateEffect(semantic.gameObject, changes, spec?.tokens);
            }
        }

        private static void ApplyFixtureTextBindings(GameObject instance, string state, UiSpec spec)
        {
            JObject semantics = StateSemantics(spec, state) as JObject;
            JObject fixtureData = semantics == null ? null : semantics["fixtureData"] as JObject;
            JArray bindings = semantics == null ? null : semantics["fixtureTextBindings"] as JArray;
            if (instance == null || fixtureData == null || bindings == null) return;
            Dictionary<string, ESUIComponentSemantic> components = instance
                .GetComponentsInChildren<ESUIComponentSemantic>(true)
                .Where(semantic => semantic != null)
                .GroupBy(semantic => semantic.gameObject.name, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            foreach (JObject binding in bindings.OfType<JObject>())
            {
                string componentId = binding.Value<string>("componentId");
                string key = binding.Value<string>("fixtureDataKey");
                string overflowPolicy = binding.Value<string>("overflowPolicy");
                int maxLines = binding.Value<int?>("maxLines") ?? 0;
                string replacement = string.IsNullOrWhiteSpace(key) ? null : fixtureData.Value<string>(key);
                if (string.IsNullOrWhiteSpace(componentId) || replacement == null || !StateTargets(spec, state, componentId)) continue;
                if (!components.TryGetValue(componentId, out ESUIComponentSemantic semantic) || semantic == null) continue;
                foreach (TMP_Text text in semantic.GetComponentsInChildren<TMP_Text>(true))
                {
                    text.text = replacement;
                    text.enableWordWrapping = overflowPolicy == "wrap";
                    text.overflowMode = overflowPolicy == "ellipsis" ? TextOverflowModes.Ellipsis : TextOverflowModes.Overflow;
                    if (maxLines > 0) text.maxVisibleLines = maxLines;
                }
            }
        }

        private static void ApplyDeclaredStateEffect(GameObject target, JObject changes, UiTokens tokens)
        {
            if (target == null || changes == null) return;
            bool? visible = changes.Value<bool?>("visible");
            if (visible.HasValue) target.SetActive(visible.Value);
            bool? interactable = changes.Value<bool?>("interactable");
            if (interactable.HasValue)
            {
                Button button = target.GetComponent<Button>();
                if (button != null) button.interactable = interactable.Value;
            }
            float? alpha = changes.Value<float?>("graphicAlpha");
            if (alpha.HasValue) SetGraphicAlpha(target, Mathf.Clamp01(alpha.Value));
            string color = changes.Value<string>("graphicColor");
            if (!string.IsNullOrWhiteSpace(color)) SetGraphicColor(target, ParseColor(color, Color.white));
            bool? wrapText = changes.Value<bool?>("wrapText");
            if (wrapText.HasValue)
                foreach (TMP_Text text in target.GetComponentsInChildren<TMP_Text>(true))
                    text.enableWordWrapping = wrapText.Value;
            string replacement = changes.Value<string>("text");
            if (!string.IsNullOrWhiteSpace(replacement))
                foreach (TMP_Text text in target.GetComponentsInChildren<TMP_Text>(true))
                    text.text = replacement;
            bool? outlineEnabled = changes.Value<bool?>("outline");
            if (outlineEnabled.HasValue)
            {
                Graphic graphic = target.GetComponent<Graphic>();
                Outline outline = target.GetComponent<Outline>();
                if (outline == null && graphic != null) outline = target.AddComponent<Outline>();
                if (outline != null)
                {
                    outline.effectColor = ParseColor(tokens == null ? null : tokens.accent, new Color(1f, 0.88f, 0.48f, 0.98f));
                    outline.effectDistance = new Vector2(3f, 3f);
                    outline.useGraphicAlpha = true;
                    outline.enabled = outlineEnabled.Value;
                }
            }
        }

        private static void ApplyFixtureStateBadge(GameObject instance, string state)
        {
            if (instance == null) return;
            const string badgeName = "__FixtureState";
            bool visible = string.Equals(state, "disabled", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "loading", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "error", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "long-content", StringComparison.OrdinalIgnoreCase);

            // The badge is evidence for the active responsive profile. Keeping it
            // directly under the instance root makes TryGetLogicalSnapshotPath skip
            // it, while putting it under the profile gives both PNG and semantic
            // snapshots the same profile/state identity.
            Transform activeProfile = null;
            foreach (string profileName in new[] { "Wide", "Narrow" })
            {
                Transform candidate = instance.transform.Find(profileName);
                if (candidate != null && candidate.gameObject.activeInHierarchy)
                {
                    activeProfile = candidate;
                    break;
                }
            }

            Transform existing = null;
            foreach (Transform candidate in instance.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name == badgeName)
                {
                    existing = candidate;
                    candidate.gameObject.SetActive(false);
                }
            }
            if (!visible || activeProfile == null) return;

            GameObject badge = existing == null ? new GameObject(badgeName, typeof(RectTransform), typeof(Image)) : existing.gameObject;
            badge.transform.SetParent(activeProfile, false);
            badge.SetActive(true);
            RectTransform rect = badge.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.sizeDelta = new Vector2(180f, 36f);
            rect.anchoredPosition = new Vector2(-24f, -20f);
            Image background = badge.GetComponent<Image>();
            background.sprite = GetGeneratedUiSprite();
            background.raycastTarget = false;
            Color color = string.Equals(state, "error", StringComparison.OrdinalIgnoreCase)
                ? ParseColor("#EF6D78", Color.red)
                : string.Equals(state, "loading", StringComparison.OrdinalIgnoreCase)
                    ? ParseColor("#F2B84B", Color.yellow)
                    : string.Equals(state, "disabled", StringComparison.OrdinalIgnoreCase)
                        ? ParseColor("#6D7C8D", Color.gray)
                        : ParseColor("#4DE1FF", Color.cyan);
            background.color = color;
            TextMeshProUGUI label = badge.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (label == null)
            {
                GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelObject.transform.SetParent(badge.transform, false);
                label = labelObject.GetComponent<TextMeshProUGUI>();
                Stretch(label.rectTransform);
                ApplyShowcaseFont(label);
                label.alignment = TextAlignmentOptions.Center;
                label.fontSize = 16f;
                label.color = Color.white;
                label.raycastTarget = false;
            }
            label.text = string.Equals(state, "disabled", StringComparison.OrdinalIgnoreCase) ? "DISABLED"
                : string.Equals(state, "loading", StringComparison.OrdinalIgnoreCase) ? "LOADING"
                : string.Equals(state, "error", StringComparison.OrdinalIgnoreCase) ? "ERROR" : "LONG CONTENT";
        }

        private static void SetGraphicAlpha(GameObject root, float alpha)
        {
            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                Color color = graphic.color; color.a = alpha; graphic.color = color;
            }
        }

        private static void SetGraphicColor(GameObject root, Color color)
        {
            Graphic graphic = root.GetComponent<Graphic>();
            if (graphic != null) graphic.color = color;
        }

        private static void ApplyFixtureProfile(GameObject instance, string profile)
        {
            if (instance == null) return;
            bool narrow = profile.IndexOf("portrait", StringComparison.OrdinalIgnoreCase) >= 0
                || profile.IndexOf("narrow", StringComparison.OrdinalIgnoreCase) >= 0;
            ESUIAdaptiveLayout adaptive = instance.GetComponent<ESUIAdaptiveLayout>();
            if (adaptive != null)
                adaptive.SetProfileOverride(true, narrow);
            Transform wide = instance.transform.Find("Wide");
            Transform narrowRoot = instance.transform.Find("Narrow");
            if (wide != null) wide.gameObject.SetActive(!narrow);
            if (narrowRoot != null) narrowRoot.gameObject.SetActive(narrow);
        }

        private static void ReapplySpecGeometry(GameObject root, UiSpec spec)
        {
            if (root == null || spec == null || spec.elements == null) return;
            foreach (string profileName in new[] { "Wide", "Narrow" })
            {
                Transform profile = root.transform.Find(profileName);
                if (profile == null) continue;
                foreach (UiElement element in spec.elements)
                {
                    if (element == null) continue;
                    if (!ShouldIncludeElement(element, profileName.Equals("Narrow", StringComparison.Ordinal))) continue;
                    ReapplyElementGeometry(profile, element, profileName.Equals("Narrow", StringComparison.Ordinal), false);
                }
            }
        }

        private static void ReapplyElementGeometry(Transform parent, UiElement element, bool narrow, bool parentManaged)
        {
            Transform child = parent.Find(element.id);
            if (child == null) return;
            UiLayoutIntent intent = narrow && element.narrowLayoutIntent != null ? element.narrowLayoutIntent : element.layoutIntent;
            ApplyLayoutIntent(child as RectTransform, child.GetComponent<LayoutElement>(), intent, parentManaged);
            bool childrenManaged = element.layoutSpec != null
                && string.Equals(element.layoutSpec.childGeometryOwner, "parent-layout-group", StringComparison.OrdinalIgnoreCase)
                && (string.Equals(element.layoutSpec.layoutMode, "vertical", StringComparison.OrdinalIgnoreCase)
                || string.Equals(element.layoutSpec.layoutMode, "horizontal", StringComparison.OrdinalIgnoreCase)
                || string.Equals(element.layoutSpec.layoutMode, "grid", StringComparison.OrdinalIgnoreCase));
            foreach (UiElement nested in element.children ?? Array.Empty<UiElement>())
                if (ShouldIncludeElement(nested, narrow)) ReapplyElementGeometry(child, nested, narrow, childrenManaged);
        }

        private static JObject BuildPngCaptureMetadata(Texture2D image, string outputPath, int byteLength)
        {
            Color32[] pixels = image == null ? Array.Empty<Color32>() : image.GetPixels32();
            int nonTransparentCount = 0;
            int opaqueCount = 0;
            int edgeTransitions = 0;
            int edgeComparisons = 0;
            int width = image == null ? 0 : image.width;
            byte minR = byte.MaxValue, minG = byte.MaxValue, minB = byte.MaxValue, minA = byte.MaxValue;
            byte maxR = byte.MinValue, maxG = byte.MinValue, maxB = byte.MinValue, maxA = byte.MinValue;
            int stride = Math.Max(1, (int)Math.Ceiling(pixels.Length / 16384d));
            var sampledBuckets = new HashSet<ushort>();
            for (int index = 0; index < pixels.Length; index++)
            {
                Color32 pixel = pixels[index];
                if (pixel.a > 0) nonTransparentCount++;
                if (pixel.a == byte.MaxValue) opaqueCount++;
                minR = Math.Min(minR, pixel.r); minG = Math.Min(minG, pixel.g); minB = Math.Min(minB, pixel.b); minA = Math.Min(minA, pixel.a);
                maxR = Math.Max(maxR, pixel.r); maxG = Math.Max(maxG, pixel.g); maxB = Math.Max(maxB, pixel.b); maxA = Math.Max(maxA, pixel.a);
                if (index % stride == 0)
                {
                    ushort bucket = (ushort)(((pixel.r >> 4) << 12) | ((pixel.g >> 4) << 8) | ((pixel.b >> 4) << 4) | (pixel.a >> 4));
                    sampledBuckets.Add(bucket);
                }
                int x = width == 0 ? 0 : index % width;
                if (x > 0)
                {
                    edgeComparisons++;
                    edgeTransitions += pixel.Equals(pixels[index - 1]) ? 0 : 1;
                }
                if (index >= width)
                {
                    edgeComparisons++;
                    edgeTransitions += pixel.Equals(pixels[index - width]) ? 0 : 1;
                }
            }
            if (pixels.Length == 0)
            {
                minR = minG = minB = minA = 0;
                maxR = maxG = maxB = maxA = 0;
            }
            return new JObject
            {
                ["pngFileName"] = Path.GetFileName(outputPath),
                ["pngSha256"] = ComputeSha256(outputPath),
                ["pngByteLength"] = byteLength,
                ["width"] = image == null ? 0 : image.width,
                ["height"] = image == null ? 0 : image.height,
                ["pixelCount"] = pixels.Length,
                ["nonTransparentPixelCount"] = nonTransparentCount,
                ["opaquePixelCount"] = opaqueCount,
                ["sampleStride"] = stride,
                ["sampledColorBucketCount"] = sampledBuckets.Count,
                ["edgeTransitionCount"] = edgeTransitions,
                ["edgeComparisonCount"] = edgeComparisons,
                ["rgbaMin"] = new JArray(minR, minG, minB, minA),
                ["rgbaMax"] = new JArray(maxR, maxG, maxB, maxA),
            };
        }

        private static void WriteEvidenceSnapshots(string root, string panelId, string profile, string state,
            int width, int height, Transform instance, RectTransform canvasRect, string contractSha256, string specHash,
            string runId, int sceneGeneration, UiSpec spec, JObject capture)
        {
            string captureKey = panelId + "." + profile + "." + state;
            JObject editor = new JObject
            {
                ["schemaVersion"] = 1,
                ["command"] = "editor.snapshot",
                ["panelId"] = panelId,
                ["profileId"] = profile,
                ["stateId"] = state,
                ["captureKey"] = captureKey,
                 ["contractSha256"] = contractSha256 ?? string.Empty,
                 ["specHash"] = specHash ?? string.Empty,
                 ["runId"] = runId ?? string.Empty,
                ["sceneGeneration"] = sceneGeneration,
                ["capture"] = capture?.DeepClone() ?? new JObject(),
                ["intentContract"] = spec?.intentContract?.DeepClone() ?? new JObject(),
                ["stateSemantics"] = StateSemantics(spec, state)?.DeepClone() ?? new JObject(),
                ["rootPath"] = "Canvas/" + instance.name,
                ["viewport"] = new JObject { ["width"] = width, ["height"] = height, ["orientation"] = width >= height ? "landscape" : "portrait" },
                ["canvas"] = CanvasMetadata(canvasRect),
                ["elements"] = BuildEditorElements(instance, width, height),
                ["maxHierarchyDepth"] = 16,
            };
            JObject runtime = new JObject
            {
                ["schemaVersion"] = 1,
                ["command"] = "ui.snapshot",
                ["panelId"] = panelId,
                ["profileId"] = profile,
                ["stateId"] = state,
                ["runId"] = runId ?? string.Empty,
                ["specHash"] = specHash ?? string.Empty,
                ["sceneGeneration"] = sceneGeneration,
                ["capture"] = capture?.DeepClone() ?? new JObject(),
                ["intentContract"] = spec?.intentContract?.DeepClone() ?? new JObject(),
                ["stateSemantics"] = StateSemantics(spec, state)?.DeepClone() ?? new JObject(),
                ["rootPath"] = "Canvas/" + instance.name,
                ["viewport"] = new JObject { ["width"] = width, ["height"] = height, ["orientation"] = width >= height ? "landscape" : "portrait" },
                ["canvas"] = CanvasMetadata(canvasRect),
                ["screenWidth"] = width,
                ["screenHeight"] = height,
                ["orientation"] = width >= height ? "landscape" : "portrait",
                ["safeArea"] = SafeAreaPixels(spec, profile, width, height),
                ["uiElements"] = BuildRuntimeElements(instance, width, height),
            };
            JObject scene = new JObject
            {
                ["schemaVersion"] = 1,
                ["command"] = "scene.snapshot",
                ["panelId"] = panelId,
                ["profileId"] = profile,
                 ["stateId"] = state,
                 ["runId"] = runId ?? string.Empty,
                 ["specHash"] = specHash ?? string.Empty,
                 ["sceneGeneration"] = sceneGeneration,
                 ["capture"] = capture?.DeepClone() ?? new JObject(),
                ["rootPath"] = "Canvas/" + instance.name,
                ["canvasPath"] = "Canvas",
            };
            WriteSnapshot(Path.Combine(root, profile + "__" + state + ".editor.json"), editor, root);
            WriteSnapshot(Path.Combine(root, profile + "__" + state + ".ui.json"), runtime, root);
            WriteSnapshot(Path.Combine(root, profile + "__" + state + ".scene.json"), scene, root);
        }

        private static JObject CanvasMetadata(RectTransform canvasRect)
        {
            Canvas canvas = canvasRect.GetComponent<Canvas>();
            CanvasScaler scaler = canvasRect.GetComponent<CanvasScaler>();
            return new JObject
            {
                ["renderMode"] = canvas == null ? "ScreenSpaceCamera" : canvas.renderMode.ToString(),
                ["scaler"] = new JObject
                {
                    ["uiScaleMode"] = scaler == null ? "ScaleWithScreenSize" : scaler.uiScaleMode.ToString(),
                    ["referenceResolution"] = new JArray(scaler == null ? 1920f : scaler.referenceResolution.x, scaler == null ? 1080f : scaler.referenceResolution.y),
                    ["screenMatchMode"] = scaler == null ? "MatchWidthOrHeight" : scaler.screenMatchMode.ToString(),
                    ["match"] = scaler == null ? 0.5f : scaler.matchWidthOrHeight,
                },
            };
        }

        private static JObject SafeAreaPixels(UiSpec spec, string profileId, int width, int height)
        {
            if (spec == null || spec.profiles == null) return new JObject { ["x"] = 0, ["y"] = 0, ["width"] = width, ["height"] = height };
            JObject authored = spec.profiles.OfType<JObject>()
                .FirstOrDefault(item => string.Equals(item.Value<string>("id"), profileId, StringComparison.OrdinalIgnoreCase));
            JArray safeArea = authored == null ? null : authored["safeArea"] as JArray;
            if (safeArea == null || safeArea.Count != 4) return new JObject { ["x"] = 0, ["y"] = 0, ["width"] = width, ["height"] = height };
            float minX = Mathf.Clamp01(safeArea[0].Value<float>());
            float minY = Mathf.Clamp01(safeArea[1].Value<float>());
            float maxX = Mathf.Clamp01(safeArea[2].Value<float>());
            float maxY = Mathf.Clamp01(safeArea[3].Value<float>());
            return new JObject
            {
                ["x"] = Mathf.RoundToInt(minX * width),
                ["y"] = Mathf.RoundToInt((1f - maxY) * height),
                ["width"] = Mathf.RoundToInt((maxX - minX) * width),
                ["height"] = Mathf.RoundToInt((maxY - minY) * height),
            };
        }

        private static JArray BuildEditorElements(Transform instance, int width, int height)
        {
            var result = new JArray();
            foreach (RectTransform rect in instance.GetComponentsInChildren<RectTransform>(true))
            {
                if (!TryGetLogicalSnapshotPath(instance, rect, out string path, out string parentPath)) continue;
                Rect screen = ScreenRect(rect, width, height);
                ESUIComponentSemantic semantic = rect.GetComponent<ESUIComponentSemantic>();
                JObject focalCrop = BuildFocalCropSnapshot(rect);
                var components = new JArray();
                foreach (Component component in rect.GetComponents<Component>())
                    if (component != null) components.Add(new JObject { ["type"] = component.GetType().Name, ["enabled"] = IsComponentEnabled(component), ["properties"] = new JObject() });
                result.Add(new JObject
                {
                    ["path"] = path,
                    ["parentPath"] = parentPath,
                    ["active"] = rect.gameObject.activeInHierarchy,
                    ["siblingIndex"] = rect.GetSiblingIndex(),
                    ["anchorMin"] = new JArray(rect.anchorMin.x, rect.anchorMin.y),
                    ["anchorMax"] = new JArray(rect.anchorMax.x, rect.anchorMax.y),
                    ["pivot"] = new JArray(rect.pivot.x, rect.pivot.y),
                    ["anchoredPosition"] = new JArray(rect.anchoredPosition.x, rect.anchoredPosition.y),
                    ["sizeDelta"] = new JArray(rect.sizeDelta.x, rect.sizeDelta.y),
                    ["screenRect"] = new JObject { ["x"] = screen.x, ["y"] = screen.y, ["width"] = screen.width, ["height"] = screen.height },
                    ["text"] = rect.GetComponent<TMP_Text>() == null ? null : rect.GetComponent<TMP_Text>().text,
                    ["componentType"] = semantic == null ? null : semantic.componentType,
                    ["visualVariant"] = semantic == null ? (rect == instance ? "default" : null) : semantic.visualVariant,
                    ["assetSlots"] = semantic == null ? new JArray() : new JArray(semantic.assetSlots ?? Array.Empty<string>()),
                    ["numericValue"] = semantic == null ? null : (semantic.hasNumericValue ? semantic.numericValue : (float?)null),
                    ["inputIntent"] = semantic == null ? null : semantic.inputIntent,
                    ["interactionTarget"] = semantic == null ? null : new JArray(semantic.interactionTargetWidth, semantic.interactionTargetHeight),
                    ["tokenRoles"] = rect == instance ? new JArray("surface", "text", "accent") : null,
                    ["assets"] = new JArray(),
                    ["components"] = components,
                    ["layout"] = BuildLayoutSnapshot(rect),
                    ["focalCrop"] = focalCrop,
                });
            }
            return result;
        }

        private static bool IsComponentEnabled(Component component)
        {
            return !(component is Behaviour behaviour) || behaviour.enabled;
        }

        private static JObject BuildLayoutSnapshot(RectTransform rect)
        {
            LayoutGroup group = rect == null ? null : rect.GetComponent<LayoutGroup>();
            if (group == null) return null;
            var directChildren = new JArray();
            for (int index = 0; index < rect.childCount; index++)
                directChildren.Add(GetRelativePath(rect, rect.GetChild(index)));
            var result = new JObject
            {
                ["type"] = group.GetType().Name,
                ["padding"] = new JArray(group.padding.left, group.padding.right, group.padding.top, group.padding.bottom),
                ["childAlignment"] = group.childAlignment.ToString(),
                ["directChildren"] = directChildren,
            };
            if (group is HorizontalOrVerticalLayoutGroup flow)
            {
                result["spacing"] = flow.spacing;
                result["controlChildWidth"] = flow.childControlWidth;
                result["controlChildHeight"] = flow.childControlHeight;
                result["forceChildExpandWidth"] = flow.childForceExpandWidth;
                result["forceChildExpandHeight"] = flow.childForceExpandHeight;
            }
            if (group is GridLayoutGroup grid)
            {
                result["columns"] = grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount ? grid.constraintCount : 0;
                result["cellSize"] = new JArray(grid.cellSize.x, grid.cellSize.y);
                result["spacing"] = new JArray(grid.spacing.x, grid.spacing.y);
            }
            return result;
        }

        private static JObject BuildFocalCropSnapshot(RectTransform rect)
        {
            ESUIFocalCropRawImage focalImage = rect == null ? null : rect.GetComponent<ESUIFocalCropRawImage>();
            if (focalImage == null) return null;
            Rect sourceUv = focalImage.SourceUv;
            Rect appliedUv = focalImage.AppliedUv;
            Rect insets = focalImage.SafeCropInsetsNormalized;
            return new JObject
            {
                ["focalPoint"] = new JArray(focalImage.FocalPoint.x, focalImage.FocalPoint.y),
                ["sourceAspectRatio"] = focalImage.SourceAspectRatio,
                ["sourceUv"] = new JObject { ["x"] = sourceUv.x, ["y"] = sourceUv.y, ["width"] = sourceUv.width, ["height"] = sourceUv.height },
                ["appliedUv"] = new JObject { ["x"] = appliedUv.x, ["y"] = appliedUv.y, ["width"] = appliedUv.width, ["height"] = appliedUv.height },
                // The serialized Rect stores [left, bottom, right, top] in x/y/width/height.
                ["safeCropInsetsNormalized"] = new JArray(insets.x, insets.y, insets.width, insets.height),
                ["safeCropSatisfied"] = focalImage.SafeCropSatisfied,
            };
        }

        private static JArray BuildRuntimeElements(Transform instance, int width, int height)
        {
            var result = new JArray();
            foreach (RectTransform rect in instance.GetComponentsInChildren<RectTransform>(true))
            {
                if (!TryGetLogicalSnapshotPath(instance, rect, out string path, out string parentPath)) continue;
                Rect screen = ScreenRect(rect, width, height);
                Button button = rect.GetComponent<Button>();
                Graphic graphic = rect.GetComponent<Graphic>();
                Graphic[] graphics = rect.GetComponentsInChildren<Graphic>(true);
                TMP_Text[] texts = rect.GetComponentsInChildren<TMP_Text>(true);
                var descendantGraphics = new JArray();
                foreach (Graphic candidate in graphics)
                {
                    descendantGraphics.Add(new JObject
                    {
                        ["path"] = GetRelativePath(rect, candidate.transform),
                        ["alpha"] = candidate.color.a,
                    });
                }
                var descendantTextStates = new JArray();
                foreach (TMP_Text candidate in texts)
                {
                    descendantTextStates.Add(new JObject
                    {
                        ["path"] = GetRelativePath(rect, candidate.transform),
                        ["wrapText"] = candidate.enableWordWrapping,
                        ["text"] = candidate.text,
                    });
                }
                float? descendantGraphicAlpha = null;
                if (graphics.Length > 0)
                {
                    float alpha = graphics[0].color.a;
                    descendantGraphicAlpha = graphics.All(candidate => Mathf.Approximately(candidate.color.a, alpha)) ? (float?)alpha : null;
                }
                bool? wrapText = null;
                string serializedText = null;
                if (texts.Length > 0)
                {
                    bool wrap = texts[0].enableWordWrapping;
                    string value = texts[0].text;
                    wrapText = texts.All(candidate => candidate.enableWordWrapping == wrap) ? (bool?)wrap : null;
                    serializedText = texts.All(candidate => string.Equals(candidate.text, value, StringComparison.Ordinal)) ? value : null;
                }
                Outline outline = rect.GetComponent<Outline>();
                ESUIComponentSemantic semantic = rect.GetComponent<ESUIComponentSemantic>();
                JObject focalCrop = BuildFocalCropSnapshot(rect);
                result.Add(new JObject
                {
                    ["path"] = path,
                    ["parentPath"] = parentPath,
                    ["active"] = rect.gameObject.activeInHierarchy,
                    ["siblingIndex"] = rect.GetSiblingIndex(),
                    ["anchorMin"] = new JArray(rect.anchorMin.x, rect.anchorMin.y),
                    ["anchorMax"] = new JArray(rect.anchorMax.x, rect.anchorMax.y),
                    ["pivot"] = new JArray(rect.pivot.x, rect.pivot.y),
                    ["hasButton"] = button != null,
                    ["interactable"] = button != null && button.interactable,
                    ["hasGraphic"] = graphic != null,
                    ["hasDescendantGraphic"] = graphics.Length > 0,
                    ["graphicAlpha"] = graphic == null ? null : (float?)graphic.color.a,
                    ["descendantGraphicAlpha"] = descendantGraphicAlpha,
                    ["descendantGraphicAlphas"] = descendantGraphics,
                    ["graphicColor"] = graphic == null ? null : "#" + ColorUtility.ToHtmlStringRGBA(graphic.color),
                    ["outline"] = outline != null && outline.enabled,
                    ["raycastTarget"] = graphic != null && graphic.raycastTarget,
                    ["hasText"] = texts.Length > 0,
                    ["wrapText"] = wrapText,
                    ["text"] = serializedText,
                    ["descendantTextStates"] = descendantTextStates,
                    ["componentType"] = semantic == null ? null : semantic.componentType,
                    ["visualVariant"] = semantic == null ? null : semantic.visualVariant,
                    ["assetSlots"] = semantic == null ? new JArray() : new JArray(semantic.assetSlots ?? Array.Empty<string>()),
                    ["numericValue"] = semantic == null ? null : (semantic.hasNumericValue ? semantic.numericValue : (float?)null),
                    ["inputIntent"] = semantic == null ? null : semantic.inputIntent,
                    ["interactionTarget"] = semantic == null ? null : new JArray(semantic.interactionTargetWidth, semantic.interactionTargetHeight),
                    ["textTruncated"] = false,
                    ["screenX"] = screen.x, ["screenY"] = screen.y,
                    ["screenWidth"] = screen.width, ["screenHeight"] = screen.height,
                    ["focalCrop"] = focalCrop,
                });
            }
            return result;
        }

        private static string GetRelativePath(Transform root, Transform target)
        {
            if (target == root) return root.name;
            var parts = new List<string>();
            for (Transform current = target; current != null && current != root; current = current.parent) parts.Add(current.name);
            parts.Reverse();
            return root.name + "/" + string.Join("/", parts);
        }

        private static bool TryGetLogicalSnapshotPath(Transform instance, Transform target, out string path, out string parentPath)
        {
            path = null;
            parentPath = null;
            if (target == instance)
            {
                path = "Canvas/" + instance.name;
                parentPath = "Canvas";
                return true;
            }
            Transform profile = target;
            while (profile.parent != null && profile.parent != instance) profile = profile.parent;
            if (profile.parent != instance || !profile.gameObject.activeInHierarchy) return false;
            var parts = new List<string>();
            for (Transform current = target; current != null && current != profile; current = current.parent) parts.Add(current.name);
            parts.Reverse();
            if (parts.Count == 0) return false;
            path = "Canvas/" + instance.name + (parts.Count == 0 ? string.Empty : "/" + string.Join("/", parts));
            parentPath = parts.Count <= 1 ? "Canvas/" + instance.name : "Canvas/" + instance.name + "/" + string.Join("/", parts.Take(parts.Count - 1));
            return true;
        }

        private static Rect ScreenRect(RectTransform rect, int width, int height)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            float minX = corners.Min(item => item.x), maxX = corners.Max(item => item.x);
            float minY = corners.Min(item => item.y), maxY = corners.Max(item => item.y);
            return new Rect(minX, minY, Mathf.Max(1f, maxX - minX), Mathf.Max(1f, maxY - minY));
        }

        private static void WriteSnapshot(string path, JObject payload, string root)
            => ES.ESManagedFileIO.WriteTextAtomic(path, payload.ToString(Formatting.Indented), new UTF8Encoding(false), root);

        private static void ValidateAssetPath(string assetPath, string requiredExtension)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.Ordinal) || assetPath.Contains("..") || assetPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                throw new InvalidDataException("输出路径必须位于 Assets/ 内且不能包含 ..：" + assetPath);
            if (!assetPath.EndsWith(requiredExtension, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("输出路径扩展名不正确：" + assetPath);
        }
    }
}
#endif
