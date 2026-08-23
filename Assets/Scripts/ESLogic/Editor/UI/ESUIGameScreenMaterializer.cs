#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using ES.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ES.Editor
{
    /// <summary>
    /// Deterministic materializer for AI-authored visual UI specs. It creates a Prefab and an isolated fixture scene.
    /// The spec is the reviewable boundary; this window never invents business data or runtime window ownership.
    /// </summary>
    internal static class ESUIGameScreenMaterializer
    {
        private static readonly HashSet<string> UiSpecFieldNames = new HashSet<string>(new[]
        {
            "panelId", "prefabPath", "fixtureScenePath", "referenceImagePath", "narrowAspectThreshold", "tokens", "designEvidence", "rootLayoutIntent", "elements",
            "background", "surface", "accent", "text", "mutedText", "danger", "titleSize", "bodySize", "buttonSize", "spacing", "padding",
            "schemaVersion", "sourceType", "brief", "analysisArtifact", "visionReview", "provider", "model", "reviewMethod", "reviewedAt", "imageHashes", "semanticCoverage", "method", "sha256", "referenceImages", "sourceRegions", "decisions", "responsiveDecisions", "assetDecisions", "assumptions",
            "path", "role", "status", "id", "bounds", "evidence", "confidence", "major", "elementId", "sourceRegionId", "layoutMode", "tokenRoles",
            "geometry", "visual", "interaction", "anchorStrategy", "sizeStrategy", "pivot", "rationale", "typographyRole", "colorRoles", "spacingRole",
            "alignment", "assetRole", "raycastPolicy", "minTarget", "profileId", "strategy", "layoutPolicy", "changes", "reason", "statement",
            "kind", "componentType", "visualVariant", "assetSlots", "value", "hasValue", "elementText", "colorToken", "layout", "width", "height", "minWidth", "minHeight", "maxWidth", "fillWidth", "interactable", "layoutIntent", "children", "layoutSpec",
            "layoutMode", "axis", "gap", "paddingLeft", "paddingRight", "paddingTop", "paddingBottom", "columns", "cellWidth", "cellHeight", "spacingX", "spacingY",
            "cellSize", "spacing", "childAlignment", "controlChildWidth", "controlChildHeight", "forceChildExpandWidth", "forceChildExpandHeight",
            "narrowLayoutIntent", "wrapText", "maxLines", "overflow", "mode", "anchorMinX", "anchorMinY", "anchorMaxX", "anchorMaxY", "pivotX",
            "pivotY", "anchoredX", "anchoredY", "sizeWidth", "sizeHeight", "ignoreParentLayout", "text"
        }, StringComparer.Ordinal);

        [Serializable] private sealed class UiSpec
        {
            public string panelId = "ui-panel";
            public string prefabPath = "Assets/UI/Prefabs/Generated/ui-panel.prefab";
            public string fixtureScenePath = "Assets/UI/Scenes/Generated/ui-panel-fixture.unity";
            public string referenceImagePath = string.Empty;
            public float narrowAspectThreshold = 1.15f;
            public UiTokens tokens = new UiTokens();
            public UiLayoutIntent rootLayoutIntent = new UiLayoutIntent();
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

        [Serializable] private sealed class UiTokens
        {
            public string background = "#121923";
            public string surface = "#1D2A39";
            public string accent = "#53B8FF";
            public string text = "#F4F7FB";
            public string mutedText = "#98A8BA";
            public string danger = "#E86C73";
            public int titleSize = 32;
            public int bodySize = 20;
            public int buttonSize = 22;
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
            public string layout = "wide";
            public float width = 0f;
            public float height = 0f;
            public float minWidth = 0f;
            public float minHeight = 0f;
            public float maxWidth = 0f;
            public bool fillWidth = true;
            public bool interactable = false;
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
                foreach (JProperty property in obj.Properties())
                {
                    if (!UiSpecFieldNames.Contains(property.Name))
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
                canvasRect.sizeDelta = new Vector2(1920f, 1080f);
                canvasRect.position = new Vector3(960f, 540f, 0f);
                Canvas c = canvas.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceCamera;
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = 0.5f;
                GameObject cameraObject = new GameObject("UI_Fixture_Camera", typeof(Camera));
                Camera fixtureCamera = cameraObject.GetComponent<Camera>(); fixtureCamera.clearFlags = CameraClearFlags.Color; fixtureCamera.backgroundColor = ParseColor(spec.tokens.background, Color.black); fixtureCamera.orthographic = true; fixtureCamera.orthographicSize = 540f; fixtureCamera.transform.position = new Vector3(960f, 540f, -1000f); fixtureCamera.transform.rotation = Quaternion.identity;
                c.worldCamera = fixtureCamera; c.planeDistance = 1000f;
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
                EnsureEventSystemInScene();
                EditorSceneManager.SaveScene(fixture, spec.fixtureScenePath);
                foreach (string state in states)
                foreach (string profile in profiles)
                {
                    bool portrait = profile.IndexOf("portrait", StringComparison.OrdinalIgnoreCase) >= 0
                        || profile.IndexOf("narrow", StringComparison.OrdinalIgnoreCase) >= 0;
                    int width = portrait ? 1080 : 1920;
                    int height = portrait ? 1920 : 1080;
                    ApplyFixtureProfile(instance, profile);
                    // Profile activation can invoke Unity Selectable lifecycle callbacks;
                    // apply the fixture state after activation so the captured Graphic is final.
                    ApplyFixtureState(instance, state);
                    string output = CaptureFixture(spec, spec.panelId, fixtureCamera, canvasRect, instance.transform,
                        width, height, profile, state, contractSha256, specHash, runId, sceneGeneration, evidenceRoot);
                    outputs.Add(output);
                }
                if (instance != null) EditorUtility.SetDirty(instance);
                AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
                return JsonUtility.ToJson(new AuthoringResult
                {
                    panelId = spec.panelId, prefabPath = spec.prefabPath, fixtureScenePath = spec.fixtureScenePath,
                    status = "Completed", elementCount = spec.elements.Length, profiles = profiles, states = states,
                    outputs = outputs.ToArray(),
                }, true);
            }
            catch
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
            finally
            {
                if (fixture.IsValid() && fixture.isLoaded)
                    EditorSceneManager.CloseScene(fixture, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildRoot(UiSpec spec)
        {
            GameObject root = new GameObject(spec.panelId, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ESUIAdaptiveLayout));
            RectTransform rr = root.GetComponent<RectTransform>(); rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one; rr.offsetMin = Vector2.zero; rr.offsetMax = Vector2.zero;
            if (spec.rootLayoutIntent != null)
            {
                rr.anchorMin = new Vector2(spec.rootLayoutIntent.anchorMinX, spec.rootLayoutIntent.anchorMinY);
                rr.anchorMax = new Vector2(spec.rootLayoutIntent.anchorMaxX, spec.rootLayoutIntent.anchorMaxY);
                rr.pivot = new Vector2(spec.rootLayoutIntent.pivotX, spec.rootLayoutIntent.pivotY);
                rr.anchoredPosition = new Vector2(spec.rootLayoutIntent.anchoredX, spec.rootLayoutIntent.anchoredY);
                if (spec.rootLayoutIntent.sizeWidth > 0f || spec.rootLayoutIntent.sizeHeight > 0f)
                    rr.sizeDelta = new Vector2(spec.rootLayoutIntent.sizeWidth, spec.rootLayoutIntent.sizeHeight);
            }
            Image rootImage = root.GetComponent<Image>(); rootImage.sprite = GetGeneratedUiSprite();
            rootImage.color = ParseColor(spec.tokens.background, Color.black);
            rootImage.raycastTarget = false;
            GameObject wide = BuildProfile(spec, "Wide", false); wide.transform.SetParent(root.transform, false);
            GameObject narrow = BuildProfile(spec, "Narrow", true); narrow.transform.SetParent(root.transform, false);
            ESUIAdaptiveLayout adaptive = root.GetComponent<ESUIAdaptiveLayout>(); adaptive.Configure(wide.transform as RectTransform, narrow.transform as RectTransform, spec.narrowAspectThreshold);
            return root;
        }

        private static GameObject BuildProfile(UiSpec spec, string profileName, bool narrow)
        {
            GameObject profile = new GameObject(profileName, typeof(RectTransform));
            RectTransform pr = profile.GetComponent<RectTransform>(); pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one; pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;
            for (int i = 0; i < spec.elements.Length; i++)
            {
                UiElement element = spec.elements[i];
                if (!ShouldIncludeElement(element, narrow)) continue;
                CreateElement(element, profile.transform, spec.tokens, narrow, false);
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

        private static void CreateElement(UiElement data, Transform parent, UiTokens tokens, bool narrow, bool parentManaged)
        {
            string kind = (data.kind ?? "text").ToLowerInvariant();
            GameObject go = new GameObject(string.IsNullOrWhiteSpace(data.id) ? kind : data.id, typeof(RectTransform)); go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            ESUIComponentSemantic semantic = go.AddComponent<ESUIComponentSemantic>();
            semantic.componentType = string.IsNullOrWhiteSpace(data.componentType) ? kind : data.componentType;
            semantic.visualVariant = string.IsNullOrWhiteSpace(data.visualVariant) ? "default" : data.visualVariant;
            semantic.assetSlots = data.assetSlots ?? Array.Empty<string>();
            semantic.numericValue = data.value;
            semantic.hasNumericValue = data.hasValue;
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
                TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>(); text.text = data.text ?? string.Empty; text.fontSize = kind == "title" ? tokens.titleSize : tokens.bodySize; text.color = ParseToken(data.colorToken, tokens, Color.white); text.alignment = TextAlignmentOptions.MidlineLeft; text.enableWordWrapping = data.wrapText; text.overflowMode = data.overflow == "wrap" ? TextOverflowModes.Overflow : data.overflow == "ellipsis" ? TextOverflowModes.Ellipsis : TextOverflowModes.Truncate; text.raycastTarget = false; if (data.maxLines > 0) text.maxVisibleLines = data.maxLines;
            }
            else if (kind == "button" || kind == "card" || kind == "panel")
            {
                Image image = go.AddComponent<Image>(); image.sprite = GetGeneratedUiSprite(); image.color = kind == "button" ? Color.white : ParseToken(data.colorToken, tokens, ParseColor(tokens.surface, Color.gray)); image.raycastTarget = kind == "button";
                if (kind == "button")
                {
                    Button button = go.AddComponent<Button>();
                    button.targetGraphic = image;
                    button.interactable = data.interactable;
                    // Fixture states own the captured color explicitly. Disable Unity's
                    // automatic ColorTint transition here so it cannot multiply the
                    // AI-authored accent a second time during an editor capture.
                    button.transition = Selectable.Transition.None;
                    Color accent = ParseToken(data.colorToken, tokens, ParseColor(tokens.accent, Color.cyan));
                    ColorBlock colors = button.colors;
                    colors.normalColor = accent;
                    colors.highlightedColor = Color.Lerp(accent, Color.white, 0.16f);
                    colors.pressedColor = Color.Lerp(accent, Color.black, 0.18f);
                    colors.selectedColor = Color.Lerp(accent, Color.white, 0.08f);
                    colors.disabledColor = new Color(accent.r, accent.g, accent.b, 0.35f);
                    button.colors = colors;
                    TextMeshProUGUI label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>(); label.transform.SetParent(go.transform, false); label.text = data.text ?? data.id; label.fontSize = tokens.buttonSize; label.color = ParseColor(tokens.text, Color.white); label.alignment = TextAlignmentOptions.Center; label.enableWordWrapping = true; label.raycastTarget = false; Stretch(label.rectTransform);
                }
                else if (!string.IsNullOrWhiteSpace(data.text))
                {
                    TextMeshProUGUI label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>(); label.transform.SetParent(go.transform, false); label.text = data.text; label.fontSize = tokens.bodySize; label.color = ParseColor(tokens.mutedText, Color.gray); label.alignment = TextAlignmentOptions.TopLeft; label.enableWordWrapping = false; label.raycastTarget = false; label.rectTransform.anchorMin = new Vector2(0.04f, 0.88f); label.rectTransform.anchorMax = new Vector2(0.96f, 0.98f); label.rectTransform.offsetMin = Vector2.zero; label.rectTransform.offsetMax = Vector2.zero;
                }
            }
            else if (kind == "spacer") size.minHeight = Mathf.Max(size.minHeight, tokens.spacing);
            else if (kind == "image") { Image image = go.AddComponent<Image>(); image.sprite = GetGeneratedUiSprite(); image.color = ParseToken(data.colorToken, tokens, Color.white); image.preserveAspect = true; }
            ConfigureContainer(go, data.layoutSpec);
            bool childrenManaged = data.layoutSpec != null && (string.Equals(data.layoutSpec.layoutMode, "vertical", StringComparison.OrdinalIgnoreCase)
                || string.Equals(data.layoutSpec.layoutMode, "horizontal", StringComparison.OrdinalIgnoreCase)
                || string.Equals(data.layoutSpec.layoutMode, "grid", StringComparison.OrdinalIgnoreCase));
            foreach (UiElement child in data.children ?? Array.Empty<UiElement>())
                if (ShouldIncludeElement(child, narrow)) CreateElement(child, go.transform, tokens, narrow, childrenManaged);
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
                case "cooldown": return BuildCooldown(go, data, tokens);
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

        private static Sprite GetGeneratedUiSprite()
        {
            const string assetPath = "Assets/UI/Generated/ESUI_WhiteSprite.asset";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite != null) return sprite;
            string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Assets/UI/Generated");
            Directory.CreateDirectory(folder);
            UnityEngine.Texture2D texture = new UnityEngine.Texture2D(2, 2, UnityEngine.TextureFormat.RGBA32, false);
            texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white }); texture.Apply(); texture.name = "ESUI_WhiteTexture";
            AssetDatabase.CreateAsset(texture, "Assets/UI/Generated/ESUI_WhiteTexture.asset");
            sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f); sprite.name = "ESUI_WhiteSprite";
            AssetDatabase.AddObjectToAsset(sprite, texture); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            return sprite;
        }

        private static TextMeshProUGUI CreateChildText(GameObject parent, string name, string value, float fontSize, Color color, float minX, float minY, float maxX, float maxY, TextAlignmentOptions alignment)
        {
            GameObject child = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); child.transform.SetParent(parent.transform, false);
            RectTransform rect = child.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(minX, minY); rect.anchorMax = new Vector2(maxX, maxY); rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            TextMeshProUGUI text = child.GetComponent<TextMeshProUGUI>(); text.text = value ?? string.Empty; text.fontSize = fontSize; text.color = color; text.alignment = alignment; text.enableWordWrapping = true; text.raycastTarget = false;
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
            rect.anchorMin = new Vector2(intent.anchorMinX, intent.anchorMinY);
            rect.anchorMax = new Vector2(intent.anchorMaxX, intent.anchorMaxY);
            rect.pivot = new Vector2(intent.pivotX, intent.pivotY);
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
        private static Color ParseToken(string token, UiTokens t, Color fallback) => ParseColor(token == "accent" ? t.accent : token == "surface" ? t.surface : token == "mutedText" ? t.mutedText : token == "danger" ? t.danger : token == "background" ? t.background : token == "text" ? t.text : token, fallback);
        private static Color ParseColor(string value, Color fallback) { if (string.IsNullOrWhiteSpace(value)) return fallback; return ColorUtility.TryParseHtmlString(value, out Color c) ? c : fallback; }
        private static void EnsureParentFolder(string assetPath) { string dir = Path.GetDirectoryName(assetPath)?.Replace('\\', '/'); if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(Path.Combine(Directory.GetParent(Application.dataPath).FullName, dir)); }
        private static void EnsureEventSystemInScene() { new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule)); }

        private static string CaptureFixture(UiSpec spec, string panelId, Camera camera, RectTransform canvasRect, Transform instance,
            int width, int height, string profile, string state, string contractSha256, string specHash, string runId, int sceneGeneration,
            string evidenceRoot)
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
            canvasRect.sizeDelta = new Vector2(width, height);
            canvasRect.position = new Vector3(width * 0.5f, height * 0.5f, 0f);
            ApplyFixtureProfile(instance.gameObject, profile);
            camera.aspect = width / (float)height;
            camera.orthographicSize = height * 0.5f;
            camera.transform.position = new Vector3(width * 0.5f, height * 0.5f, -1000f);
            camera.targetTexture = target;
            camera.enabled = true;
            Canvas.ForceUpdateCanvases();
            ApplyFixtureProfile(instance.gameObject, profile);
            ReapplySpecGeometry(instance.gameObject, spec);
            Canvas.ForceUpdateCanvases();
            ReapplySpecGeometry(instance.gameObject, spec);
            RenderTexture.active = target;
            GL.Clear(true, true, camera.backgroundColor);
            camera.Render();
            Texture2D image = new Texture2D(width, height, UnityEngine.TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            image.Apply();
            string outputPath = Path.Combine(root, profile + "__" + state + ".png");
            ES.ESManagedFileIO.WriteBytesAtomic(outputPath, image.EncodeToPNG(), root);
            WriteEvidenceSnapshots(root, panelId, profile, state, width, height, instance,
                canvasRect, contractSha256, specHash, runId, sceneGeneration);
            UnityEngine.Object.DestroyImmediate(image);
            camera.targetTexture = null;
            RenderTexture.active = previous;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            return outputPath.Replace('\\', '/');
        }

        private static void ApplyFixtureState(GameObject instance, string state)
        {
            bool selected = string.Equals(state, "selected", StringComparison.OrdinalIgnoreCase);
            bool disabled = string.Equals(state, "disabled", StringComparison.OrdinalIgnoreCase);
            bool empty = string.Equals(state, "empty", StringComparison.OrdinalIgnoreCase);
            bool loading = string.Equals(state, "loading", StringComparison.OrdinalIgnoreCase);
            bool error = string.Equals(state, "error", StringComparison.OrdinalIgnoreCase);
            bool longContent = string.Equals(state, "long-content", StringComparison.OrdinalIgnoreCase);
            foreach (ESUIComponentSemantic semantic in instance.GetComponentsInChildren<ESUIComponentSemantic>(true))
            {
                if (semantic == null) continue;
                Outline outline = semantic.GetComponent<Outline>();
                if (outline != null) outline.enabled = selected || string.Equals(semantic.visualVariant, "selected", StringComparison.OrdinalIgnoreCase);
                string type = (semantic.componentType ?? string.Empty).ToLowerInvariant();
                bool hideData = empty && (type == "item-slot" || type == "item-card" || type == "list" || type == "grid");
                foreach (Transform child in semantic.transform)
                {
                    if (child.name == "Icon" || child.name == "Name" || child.name == "Rarity" || child.name == "Quantity") child.gameObject.SetActive(!hideData);
                    if (child.name == "State" && (loading || error || empty)) child.gameObject.SetActive(true);
                }
                if (loading && (type == "loading" || type == "progress" || type == "bar"))
                    SetGraphicAlpha(semantic.gameObject, 0.72f);
                else if (error && (type == "error-state" || type == "status-badge"))
                    SetGraphicColor(semantic.gameObject, ParseColor("#E86C73", Color.red));
                else if (disabled) SetGraphicAlpha(semantic.gameObject, 0.45f);
                if (longContent)
                    foreach (TMP_Text text in semantic.GetComponentsInChildren<TMP_Text>(true)) text.enableWordWrapping = true;
            }
            foreach (Button button in instance.GetComponentsInChildren<Button>(true))
            {
                Image image = button.GetComponent<Image>();
                if (image == null) continue;
                ColorBlock colors = button.colors;
                // The fixture owns the captured state because ColorTint is disabled above.
                image.color = selected ? colors.selectedColor : colors.normalColor;
                button.interactable = !disabled;
            }
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
            Image image = root.GetComponent<Image>();
            if (image != null) image.color = color;
        }

        private static void ApplyFixtureProfile(GameObject instance, string profile)
        {
            if (instance == null) return;
            bool narrow = profile.IndexOf("portrait", StringComparison.OrdinalIgnoreCase) >= 0
                || profile.IndexOf("narrow", StringComparison.OrdinalIgnoreCase) >= 0;
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
            bool childrenManaged = element.layoutSpec != null && (string.Equals(element.layoutSpec.layoutMode, "vertical", StringComparison.OrdinalIgnoreCase)
                || string.Equals(element.layoutSpec.layoutMode, "horizontal", StringComparison.OrdinalIgnoreCase)
                || string.Equals(element.layoutSpec.layoutMode, "grid", StringComparison.OrdinalIgnoreCase));
            foreach (UiElement nested in element.children ?? Array.Empty<UiElement>())
                if (ShouldIncludeElement(nested, narrow)) ReapplyElementGeometry(child, nested, narrow, childrenManaged);
        }

        private static void WriteEvidenceSnapshots(string root, string panelId, string profile, string state,
            int width, int height, Transform instance, RectTransform canvasRect, string contractSha256, string specHash,
            string runId, int sceneGeneration)
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
                ["screenWidth"] = width,
                ["screenHeight"] = height,
                ["orientation"] = width >= height ? "landscape" : "portrait",
                ["safeArea"] = new JObject { ["x"] = 0, ["y"] = 0, ["width"] = width, ["height"] = height },
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

        private static JArray BuildEditorElements(Transform instance, int width, int height)
        {
            var result = new JArray();
            foreach (RectTransform rect in instance.GetComponentsInChildren<RectTransform>(true))
            {
                if (!TryGetLogicalSnapshotPath(instance, rect, out string path, out string parentPath)) continue;
                Rect screen = ScreenRect(rect, width, height);
                ESUIComponentSemantic semantic = rect.GetComponent<ESUIComponentSemantic>();
                var components = new JArray();
                foreach (Component component in rect.GetComponents<Component>())
                    if (component != null) components.Add(new JObject { ["type"] = component.GetType().Name, ["enabled"] = true, ["properties"] = new JObject() });
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
                    ["tokenRoles"] = rect == instance ? new JArray("surface", "text", "accent") : null,
                    ["assets"] = new JArray(),
                    ["components"] = components,
                    ["layout"] = BuildLayoutSnapshot(rect),
                });
            }
            return result;
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

        private static JArray BuildRuntimeElements(Transform instance, int width, int height)
        {
            var result = new JArray();
            foreach (RectTransform rect in instance.GetComponentsInChildren<RectTransform>(true))
            {
                if (!TryGetLogicalSnapshotPath(instance, rect, out string path, out _)) continue;
                Rect screen = ScreenRect(rect, width, height);
                Button button = rect.GetComponent<Button>();
                Graphic graphic = rect.GetComponent<Graphic>();
                TMP_Text text = rect.GetComponent<TMP_Text>();
                ESUIComponentSemantic semantic = rect.GetComponent<ESUIComponentSemantic>();
                result.Add(new JObject
                {
                    ["path"] = path,
                    ["active"] = rect.gameObject.activeInHierarchy,
                    ["interactable"] = button != null && button.interactable,
                    ["raycastTarget"] = graphic != null && graphic.raycastTarget,
                    ["text"] = text == null ? null : text.text,
                    ["componentType"] = semantic == null ? null : semantic.componentType,
                    ["visualVariant"] = semantic == null ? null : semantic.visualVariant,
                    ["assetSlots"] = semantic == null ? new JArray() : new JArray(semantic.assetSlots ?? Array.Empty<string>()),
                    ["numericValue"] = semantic == null ? null : (semantic.hasNumericValue ? semantic.numericValue : (float?)null),
                    ["textTruncated"] = false,
                    ["screenX"] = screen.x, ["screenY"] = screen.y,
                    ["screenWidth"] = screen.width, ["screenHeight"] = screen.height,
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
